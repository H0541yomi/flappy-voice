using System;

namespace FlappyVoice.Gameplay
{
    public sealed class OctaveAnchor
    {
        private float _captureWindowSec = 0.35f;
        private float _toleranceSemitones = 1.5f;
        private float _minMidi;
        private float _maxMidi;

        private double _candidateSum;
        private int _candidateCount;
        private float _candidateMidi;
        private float _accumulatedSec;

        public bool IsAnchored { get; private set; }
        public float FloorMidi { get; private set; }

        public OctaveAnchor()
        {
            _minMidi = PitchMath.HzToMidi(70f);
            _maxMidi = PitchMath.HzToMidi(700f);
        }

        public void Configure(float captureWindowSec, float stabilityToleranceSemitones, float clampMinHz, float clampMaxHz)
        {
            _captureWindowSec = captureWindowSec > 0f ? captureWindowSec : 0f;
            _toleranceSemitones = stabilityToleranceSemitones > 0f ? stabilityToleranceSemitones : 0f;

            float lo = PitchMath.HzToMidi(clampMinHz);
            float hi = PitchMath.HzToMidi(clampMaxHz);
            if (hi < lo)
            {
                float swap = lo;
                lo = hi;
                hi = swap;
            }
            _minMidi = lo;
            _maxMidi = hi;

            Reset();
        }

        public bool TryCapture(float hz, float deltaTime)
        {
            if (IsAnchored) return false;

            if (hz <= 0f)
            {
                ClearProgress();
                return false;
            }

            float midi = PitchMath.HzToMidi(hz);

            if (_candidateCount == 0)
            {
                StartCandidate(midi);
            }
            else if (Math.Abs(midi - _candidateMidi) > _toleranceSemitones)
            {
                StartCandidate(midi);
                return false;
            }
            else
            {
                _candidateSum += midi;
                _candidateCount++;
                _candidateMidi = (float)(_candidateSum / _candidateCount);
            }

            if (deltaTime > 0f) _accumulatedSec += deltaTime;

            if (_accumulatedSec < _captureWindowSec) return false;

            SetFloorMidi(_candidateMidi);
            ClearProgress();
            return true;
        }

        public void SetFloorMidi(float midi)
        {
            // Round up by a half semitone first: a first note detected a few cents flat of an A is
            // still that A, and snapping strictly downward would drop the floor a whole octave and
            // pin the singer's entire range to the top of the screen.
            FloorMidi = ClampAToVocalRange(PitchMath.NearestAFloorAtOrBelow(midi + 0.5f));
            IsAnchored = true;
        }

        public void Reset()
        {
            IsAnchored = false;
            FloorMidi = 0f;
            ClearProgress();
        }

        // A cough or a thump must not become the anchor, but the floor also has to stay exactly on an
        // A or every pipe's note letter is wrong. So the vocal-range clamp may only move the floor by
        // WHOLE octaves, never by an arbitrary amount.
        private float ClampAToVocalRange(float aMidi)
        {
            const float octave = 12f;
            if (aMidi < _minMidi)
            {
                aMidi += octave * (float)Math.Ceiling((_minMidi - aMidi) / octave);
            }
            if (aMidi > _maxMidi)
            {
                aMidi -= octave * (float)Math.Ceiling((aMidi - _maxMidi) / octave);
            }
            return aMidi;
        }

        private void StartCandidate(float midi)
        {
            _candidateSum = midi;
            _candidateCount = 1;
            _candidateMidi = midi;
            _accumulatedSec = 0f;
        }

        private void ClearProgress()
        {
            _candidateSum = 0.0;
            _candidateCount = 0;
            _candidateMidi = 0f;
            _accumulatedSec = 0f;
        }
    }
}
