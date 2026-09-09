using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using UnityEngine;

namespace FlappyVoice.Audio
{
    public sealed class PitchTracker : MonoBehaviour
    {
        private const float NoteChangeToleranceSemitones = 1f;

        [SerializeField] private MicrophoneInput microphoneInput;

        // Detection range is NOT the anchor's vocal-range sanity clamp: clamping detection at 700Hz
        // makes anything above ~F5 read as unvoiced and freezes the character mid-song.
        [SerializeField] private float _detectorMinHz = 70f;
        [SerializeField] private float _detectorMaxHz = 1200f;

        // ~4 frames at 60fps (~67ms): rides out a consonant or breath without paying the 80ms
        // re-sustain, and without restarting the attract-mode anchor capture window.
        [SerializeField] private int _dropoutHoldFrames = 4;

        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        private YinPitchDetector _detector;
        private float[] _buffer;

        private int _pitchBufferSize = 2048;
        private float _amplitudeGateRms = 0.015f;
        private float _sustainMs = 80f;
        private float _yinThreshold = 0.15f;
        private int _detectorSampleRate;

        private float _acceptedHz;
        private float _candidateHz;
        private float _candidateHeldMs;
        private bool _hasCandidate;
        private int _unvoicedFrames;

        public PitchSample Current { get; private set; }
        public bool HasVoice => Current.IsVoiced;
        public event System.Action<PitchSample> OnSample;

        public void Configure(GameConfig config)
        {
            if (config == null) return;

            _pitchBufferSize = config.PitchBufferSize > 64 ? config.PitchBufferSize : 2048;
            _amplitudeGateRms = config.AmplitudeGateRms;
            _sustainMs = config.SustainMs;
            _yinThreshold = config.YinThreshold;

            if (_buffer == null || _buffer.Length != _pitchBufferSize) _buffer = new float[_pitchBufferSize];
            _detector = null;
            ResetTracking();
        }

        public void SetMicrophoneInput(MicrophoneInput input)
        {
            microphoneInput = input;
            _detector = null;
            ResetTracking();
        }

        private void Update()
        {
            if (microphoneInput == null || !microphoneInput.IsRecording)
            {
                ResetTracking();
                Emit(0f, 0f, 0f, false);
                return;
            }

            EnsureDetector(microphoneInput.SampleRate);

            int count = microphoneInput.ReadLatest(_buffer);
            if (count <= 0)
            {
                HandleUnvoicedFrame(0f);
                return;
            }

            float rms = YinPitchDetector.ComputeRms(_buffer, 0, count);
            if (rms < _amplitudeGateRms)
            {
                HandleUnvoicedFrame(rms);
                return;
            }

            float confidence;
            float hz = _detector.Detect(_buffer, 0, count, out confidence);
            if (hz <= 0f)
            {
                HandleUnvoicedFrame(rms);
                return;
            }

            _unvoicedFrames = 0;
            float midi = PitchMath.HzToMidi(hz);

            if (_acceptedHz > 0f && Mathf.Abs(midi - PitchMath.HzToMidi(_acceptedHz)) <= NoteChangeToleranceSemitones)
            {
                _acceptedHz = hz;
                _hasCandidate = false;
                _candidateHeldMs = 0f;
                Emit(hz, rms, confidence, true);
                return;
            }

            if (!_hasCandidate || Mathf.Abs(midi - PitchMath.HzToMidi(_candidateHz)) > NoteChangeToleranceSemitones)
            {
                _hasCandidate = true;
                _candidateHz = hz;
                _candidateHeldMs = 0f;
            }
            else
            {
                _candidateHeldMs += Time.deltaTime * 1000f;
                _candidateHz = hz;
            }

            if (_candidateHeldMs >= _sustainMs)
            {
                _acceptedHz = hz;
                _hasCandidate = false;
                _candidateHeldMs = 0f;
                Emit(hz, rms, confidence, true);
                return;
            }

            Emit(_acceptedHz, rms, confidence, _acceptedHz > 0f);
        }

        private void EnsureDetector(int sampleRate)
        {
            if (sampleRate <= 0) sampleRate = 44100;
            if (_buffer == null || _buffer.Length != _pitchBufferSize) _buffer = new float[_pitchBufferSize];
            if (_detector != null && _detectorSampleRate == sampleRate) return;

            _detectorSampleRate = sampleRate;
            _detector = new YinPitchDetector(_pitchBufferSize, sampleRate, _detectorMinHz, _detectorMaxHz, _yinThreshold);
        }

        private void HandleUnvoicedFrame(float rms)
        {
            if (_acceptedHz > 0f && _unvoicedFrames < _dropoutHoldFrames)
            {
                _unvoicedFrames++;
                Emit(_acceptedHz, rms, 0f, true);
                return;
            }

            ResetTracking();
            Emit(0f, rms, 0f, false);
        }

        private void ResetTracking()
        {
            _acceptedHz = 0f;
            _candidateHz = 0f;
            _candidateHeldMs = 0f;
            _hasCandidate = false;
            _unvoicedFrames = 0;
        }

        private void Emit(float hz, float amplitude, float confidence, bool isVoiced)
        {
            PitchSample sample;
            sample.TimestampMs = _clock.ElapsedMilliseconds;
            sample.FrequencyHz = hz;
            sample.Amplitude = amplitude;
            sample.Confidence = confidence;
            sample.IsVoiced = isVoiced;

            Current = sample;
            if (OnSample != null) OnSample(sample);
        }
    }
}
