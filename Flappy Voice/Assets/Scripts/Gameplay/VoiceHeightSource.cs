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

        // Used by dev mode, which drives the bird from a slider and therefore never sings a note to
        // anchor with. Without a floor there is no note letter to put on a pipe or on the note bar.
        public void ForceAnchor(float centerMidi)
        {
            _anchor.SetCenterMidi(centerMidi);
            OnAnchorChanged?.Invoke();
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
                RequestHandoffCentering(false);
                IsActive = false;
                CurrentMidi = -1f;
                return;
            }

            float deltaTime = Time.deltaTime;
            float midi = PitchMath.HzToMidi(sample.FrequencyHz);
            CurrentMidi = midi;

            if (!_anchor.IsAnchored)
            {
                RequestHandoffCentering(true);

                // The first sung note becomes the CENTRE of the range, so the handoff lands the bird
                // at mid-screen - which is exactly where the attract pilot is being eased to. That
                // makes the snap at handoff near-zero instead of up to half a screen.
                if (!_anchor.TryCapture(sample.FrequencyHz, deltaTime))
                {
                    IsActive = false;
                    return;
                }

                RequestHandoffCentering(false);
                OnAnchorChanged?.Invoke();
            }

            // TODO: AdaptiveRecenterer is still deliberately not wired in. Drifting the floor mid-run
            // would silently relabel every pipe already on screen, so re-enable it only together with
            // a rule for what the letters do while the floor moves.

            _height = PitchMath.ClampToOctaveHeight(midi, _anchor.FloorMidi, _config.OctaveWidthSemitones);
            IsActive = true;
        }

        private void RequestHandoffCentering(bool centering)
        {
            if (_attractPilot != null) _attractPilot.SetHandoffCentering(centering);
        }
    }
}
