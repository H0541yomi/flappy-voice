using System;

namespace FlappyVoice.Gameplay
{
    // Anchors the playable range around the first note the player sings. The note is rounded to the
    // nearest semitone and becomes the CENTRE of the screen; the range then runs half a span below
    // and half a span above it. Notes outside that span clamp to the floor/ceiling - there is no
    // wrap. Anchoring on the sung note rather than on a fixed letter is what makes the range
    // reachable no matter which note the player happens to start on.
    public sealed class OctaveAnchor
    {
        private float _captureWindowSec = 0.35f;
        private float _toleranceSemitones = 1.5f;
        private int _rangeWidthSemitones = 12;
        private float _minMidi;
        private float _maxMidi;

        private double _candidateSum;
        private int _candidateCount;
        private float _candidateMidi;
        private float _accumulatedSec;

        public bool IsAnchored { get; private set; }
        public float FloorMidi { get; private set; }
        public float CenterMidi { get; private set; }
        public float CeilingMidi => FloorMidi + _rangeWidthSemitones;

        public OctaveAnchor()
        {
            _minMidi = PitchMath.HzToMidi(70f);
            _maxMidi = PitchMath.HzToMidi(700f);
        }

        public void Configure(float captureWindowSec, float stabilityToleranceSemitones, float clampMinHz,
            float clampMaxHz, int rangeWidthSemitones)
        {
            _captureWindowSec = captureWindowSec > 0f ? captureWindowSec : 0f;
            _toleranceSemitones = stabilityToleranceSemitones > 0f ? stabilityToleranceSemitones : 0f;
            _rangeWidthSemitones = rangeWidthSemitones > 0 ? rangeWidthSemitones : 12;

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

            SetCenterMidi(_candidateMidi);
            ClearProgress();
            return true;
        }

        // The centre is clamped into the plausible vocal range BEFORE rounding, so a cough or a thump
        // cannot anchor the range an octave away from anything the player can actually sing.
        public void SetCenterMidi(float midi)
        {
            float clamped = midi < _minMidi ? _minMidi : (midi > _maxMidi ? _maxMidi : midi);
            CenterMidi = PitchMath.RoundToSemitone(clamped);
            FloorMidi = CenterMidi - PitchMath.SemitonesBelowCenter(_rangeWidthSemitones);
            IsAnchored = true;
        }

        public void Reset()
        {
            IsAnchored = false;
            FloorMidi = 0f;
            CenterMidi = 0f;
            ClearProgress();
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
