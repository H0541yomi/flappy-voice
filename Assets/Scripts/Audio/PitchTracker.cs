using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using UnityEngine;

namespace FlappyVoice.Audio
{
    public sealed class PitchTracker : MonoBehaviour
    {
        private const float NoteChangeToleranceSemitones = 1f;

        // The two ends of the sensitivity slider, as RMS amplitude floors. Below the floor a
        // frame is called silence and never reaches the detector, so this is the one number that
        // decides what counts as singing at all - see the note on AmplitudeGateRms.
        //
        // The ends are picked so that each one is a DIFFERENT KIND of microphone, not two
        // settings a room's noise sits either side of. At the sensitive end (-86 dBFS) the gate
        // is under the self-noise of a phone mic, so room tone alone is enough to fly the bird -
        // which is the point: a player who cannot be heard can always turn it up until
        // something happens. At the other (-10.5 dBFS) it takes a voice raised right at the
        // phone, which is what a loud room needs.
        //
        // This span replaced a much narrower 0.0002..0.02: 40 dB sounds wide written down, but
        // its quiet end was already past every real room and its loud end was ordinary speech,
        // so the two extremes behaved the same in most rooms and the slider read as doing
        // nothing.
        //
        // Both ends are authored in dBFS and written here as the amplitude they mean, because
        // the gate is compared against an RMS: 10^(-86/20) and 10^(-10.5/20).
        public const float MostSensitiveGateRms = 0.00005f;
        public const float LeastSensitiveGateRms = 0.2985f;

        // Where GameConfig's authored 0.001 gate falls on that slider. Kept as the one default so
        // a player who never touches the slider gets exactly the tuning the game shipped with -
        // MicSensitivityTests pins that, so this moves whenever either end above does.
        //
        // The cost of the wider span, stated rather than discovered: 75.5 dB over the travel is
        // 0.76 dB per percent, so the usable band around the default is a smaller slice of the
        // bar than it was. That is the trade for ends that actually differ.
        public const float DefaultSensitivity01 = 0.655f;

        [SerializeField] private MicrophoneInput microphoneInput;

        // Detection range is NOT the anchor's vocal-range sanity clamp: clamping detection at 700Hz
        // makes anything above ~F5 read as unvoiced and freezes the character mid-song.
        [SerializeField] private float _detectorMinHz = 70f;
        [SerializeField] private float _detectorMaxHz = 1200f;

        // ~4 frames at 60fps (~67ms): rides out a consonant or breath without paying the
        // re-sustain, and without restarting the attract-mode anchor capture window.
        [SerializeField] private int _dropoutHoldFrames = 4;

        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        private YinPitchDetector _detector;
        private float[] _buffer;

        private int _pitchBufferSize = 2048;
        private float _amplitudeGateRms = 0.03f;
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

        /// <summary>
        /// The RMS amplitude a frame has to reach before it is pitched at all. Settable at
        /// runtime because no single value suits both a quiet room and a noisy one: too low and
        /// room noise gets a pitch and flies the bird, too high and a soft singer is silence.
        /// </summary>
        public float AmplitudeGateRms
        {
            get => _amplitudeGateRms;
            set => _amplitudeGateRms = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Maps a 0..1 "sensitivity" - 1 being the most sensitive - onto the amplitude gate.
        /// Geometric rather than linear because loudness is: the bottom of a linear sweep would
        /// spend most of its travel on gates no voice ever trips.
        /// </summary>
        public static float GateRmsForSensitivity(float sensitivity01)
        {
            return LeastSensitiveGateRms * Mathf.Pow(MostSensitiveGateRms / LeastSensitiveGateRms,
                Mathf.Clamp01(sensitivity01));
        }

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
