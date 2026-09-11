using System;

namespace FlappyVoice.Gameplay
{
    // Anchors the playable range around the first note the player sings. The note is rounded to the
    // nearest semitone and placed at a REQUESTED height on screen - normally the height of the gap
    // the bird is flying at, so the note that starts the run is also the note that threads the
    // first pipe. The range then runs from there: whatever is left below the note goes below it,
    // the rest above. Notes outside that span clamp to the floor/ceiling - there is no wrap.
    // Anchoring on the sung note rather than on a fixed letter is what makes the range reachable
    // no matter which note the player happens to start on.
    public sealed class OctaveAnchor
    {
        private float _captureWindowSec = 0.35f;
        private float _toleranceSemitones = 1.5f;
        private int _rangeWidthSemitones = 24;
        private float _minMidi;
        private float _maxMidi;

        private double _candidateSum;
        private int _candidateCount;
        private float _candidateMidi;
        private float _accumulatedSec;

        // Height the sung note is placed at when nothing better is known: dead centre, the old
        // unconditional behaviour.
        private const float DefaultAnchorHeight = 0.5f;

        public bool IsAnchored { get; private set; }
        public float FloorMidi { get; private set; }
        public float CeilingMidi => FloorMidi + _rangeWidthSemitones;

        // The middle note of the range. Derived, not stored: the sung note no longer has to be it.
        public float CenterMidi => FloorMidi + PitchMath.SemitonesBelowCenter(_rangeWidthSemitones);

        // Note that was captured, and where on screen it was placed. Kept for the read-outs and to
        // make the anchoring decision inspectable after the fact.
        public float AnchorMidi { get; private set; }
        public float AnchorHeight01 { get; private set; } = DefaultAnchorHeight;

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
            _rangeWidthSemitones = rangeWidthSemitones > 0 ? rangeWidthSemitones : 24;

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
            return TryCapture(hz, deltaTime, DefaultAnchorHeight);
        }

        // targetHeight01 is where the note being captured should end up on screen. It is read at
        // the moment of capture rather than at the start of the window, so a pipe that moves while
        // the player is holding the note is aimed at where it will be, not where it was.
        public bool TryCapture(float hz, float deltaTime, float targetHeight01)
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

            SetNoteAtHeight(_candidateMidi, targetHeight01);
            ClearProgress();
            return true;
        }

        public void SetCenterMidi(float midi)
        {
            SetNoteAtHeight(midi, DefaultAnchorHeight);
        }

        // The note is clamped into the plausible vocal range BEFORE rounding, so a cough or a thump
        // cannot anchor the range an octave away from anything the player can actually sing.
        public void SetNoteAtHeight(float midi, float height01)
        {
            float clamped = midi < _minMidi ? _minMidi : (midi > _maxMidi ? _maxMidi : midi);
            AnchorMidi = PitchMath.RoundToSemitone(clamped);
            AnchorHeight01 = Math.Min(1f, Math.Max(0f, height01));
            FloorMidi = PitchMath.FloorMidiForNoteAtHeight(AnchorMidi, AnchorHeight01, _rangeWidthSemitones);
            IsAnchored = true;
        }

        public void Reset()
        {
            IsAnchored = false;
            FloorMidi = 0f;
            AnchorMidi = 0f;
            AnchorHeight01 = DefaultAnchorHeight;
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
