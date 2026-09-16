using System;
using FlappyVoice.Audio;
using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class VoiceHeightSource : MonoBehaviour, IHeightSource
    {
        private const float FallbackHandoffHeight = 0.5f;

        // Wider than any vibrato, narrower than the smallest deliberate move. It matches
        // PitchTracker's own NoteChangeToleranceSemitones on purpose: a jump the tracker made
        // the singer hold SustainMs to prove is a new note, and a new note must not be eased into.
        private const float PitchSnapSemitones = 1f;

        [SerializeField] private AttractPilot _attractPilot;

        private readonly OctaveAnchor _anchor = new OctaveAnchor();

        private GameConfig _config;
        private PitchTracker _tracker;
        private float _height = 0.5f;
        private float _smoothedMidi = -1f;
        private float _midiSmoothVelocity;

        // Raised the moment the range is anchored (or re-anchored). Pipe note letters are derived
        // from the floor, so anything already on screen has to be relabelled when the floor moves.
        public event Action OnAnchorChanged;

        public OctaveAnchor Anchor => _anchor;
        public bool IsAnchored => _anchor.IsAnchored;
        public float FloorMidi => _anchor.FloorMidi;
        public float TargetHeight01 => _height;
        public bool IsActive { get; private set; }

        // Last voiced pitch in MIDI, or -1 when nothing is being sung. Drives the note read-out bar.
        public float CurrentMidi { get; private set; } = -1f;

        private void Awake()
        {
            if (_attractPilot == null) _attractPilot = GetComponent<AttractPilot>();
            if (_attractPilot == null) _attractPilot = FindFirstObjectByType<AttractPilot>();
        }

        public void Configure(GameConfig config, PitchTracker tracker)
        {
            _config = config;
            _tracker = tracker;

            if (_config == null)
            {
                return;
            }

            _anchor.Configure(
                _config.AnchorCaptureWindowMs / 1000f,
                _config.AnchorStabilityToleranceSemitones,
                _config.VocalRangeClampMinHz,
                _config.VocalRangeClampMaxHz,
                _config.OctaveWidthSemitones);
        }

        public void ResetForNewRun()
        {
            _anchor.Reset();
            _height = FallbackHandoffHeight;
            CurrentMidi = -1f;
            _smoothedMidi = -1f;
            IsActive = false;

            if (_attractPilot != null)
            {
                _attractPilot.ResetState();
            }

            OnAnchorChanged?.Invoke();
        }

        private void Update()
        {
            if (_config == null || _tracker == null)
            {
                IsActive = false;
                CurrentMidi = -1f;
                _smoothedMidi = -1f;
                return;
            }

            PitchSample sample = _tracker.Current;
            if (!sample.IsVoiced || sample.FrequencyHz <= 0f)
            {
                IsActive = false;
                CurrentMidi = -1f;
                _smoothedMidi = -1f;
                return;
            }

            float deltaTime = Time.deltaTime;
            float midi = SmoothMidi(PitchMath.HzToMidi(sample.FrequencyHz), deltaTime);
            CurrentMidi = midi;

            if (!_anchor.IsAnchored)
            {
                // The first sung note is placed at the height of the gap the attract pilot is
                // flying at, not at mid-screen: the note that starts the run is then the note that
                // threads the first pipe, and the bird is already sitting where the handoff will
                // put it, so there is nothing to snap.
                if (!_anchor.TryCapture(sample.FrequencyHz, deltaTime, HandoffTargetHeight()))
                {
                    IsActive = false;
                    return;
                }

                OnAnchorChanged?.Invoke();
            }

            // TODO: AdaptiveRecenterer is still deliberately not wired in. Drifting the floor mid-run
            // would silently relabel every pipe already on screen, so re-enable it only together with
            // a rule for what the letters do while the floor moves.

            _height = PitchMath.ClampToOctaveHeight(midi, _anchor.FloorMidi, _config.OctaveWidthSemitones);
            IsActive = true;
        }

        /// <summary>
        /// Steadies what the dial and the bird are shown, because a singer holding one note still
        /// measures as a shimmer of pitches and the screen reads that as a shake rather than as a
        /// held note. Eases towards the measured pitch, and hands back moves wider than vibrato
        /// untouched so changing note is as immediate as it ever was.
        /// </summary>
        private float SmoothMidi(float midi, float deltaTime)
        {
            if (_smoothedMidi < 0f || Mathf.Abs(midi - _smoothedMidi) > PitchSnapSemitones)
            {
                _midiSmoothVelocity = 0f;
                _smoothedMidi = midi;
                return midi;
            }

            _smoothedMidi = Mathf.SmoothDamp(_smoothedMidi, midi, ref _midiSmoothVelocity,
                _config.PitchSmoothTimeSec, Mathf.Infinity, deltaTime);
            return _smoothedMidi;
        }

        // No pipe on screen to aim at - the very first frames of a run, or a run that starts before
        // the first spawn - so there is nothing better than the middle of the range.
        private float HandoffTargetHeight()
        {
            if (_attractPilot != null && _attractPilot.TryGetGapTargetHeight(out float gapHeight))
            {
                return gapHeight;
            }

            return FallbackHandoffHeight;
        }
    }
}
