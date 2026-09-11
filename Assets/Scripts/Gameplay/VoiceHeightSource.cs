using System;
using FlappyVoice.Audio;
using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class VoiceHeightSource : MonoBehaviour, IHeightSource
    {
        private const float FallbackHandoffHeight = 0.5f;

        [SerializeField] private AttractPilot _attractPilot;

        private readonly OctaveAnchor _anchor = new OctaveAnchor();

        private GameConfig _config;
        private PitchTracker _tracker;
        private float _height = 0.5f;

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
                return;
            }

            PitchSample sample = _tracker.Current;
            if (!sample.IsVoiced || sample.FrequencyHz <= 0f)
            {
                IsActive = false;
                CurrentMidi = -1f;
                return;
            }

            float deltaTime = Time.deltaTime;
            float midi = PitchMath.HzToMidi(sample.FrequencyHz);
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
