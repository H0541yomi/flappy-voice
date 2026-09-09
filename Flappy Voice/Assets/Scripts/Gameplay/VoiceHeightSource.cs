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

        public OctaveAnchor Anchor => _anchor;
        public bool IsAnchored => _anchor.IsAnchored;
        public float TargetHeight01 => _height;
        public bool IsActive { get; private set; }

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
                _config.VocalRangeClampMaxHz);
        }

        public void ResetForNewRun()
        {
            _anchor.Reset();
            _height = FallbackHandoffHeight;
            IsActive = false;

            if (_attractPilot != null)
            {
                _attractPilot.ResetState();
            }
        }

        private void Update()
        {
            if (_config == null || _tracker == null)
            {
                IsActive = false;
                return;
            }

            PitchSample sample = _tracker.Current;
            if (!sample.IsVoiced || sample.FrequencyHz <= 0f)
            {
                RequestHandoffCentering(false);
                IsActive = false;
                return;
            }

            float deltaTime = Time.deltaTime;
            float midi = PitchMath.HzToMidi(sample.FrequencyHz);

            if (!_anchor.IsAnchored)
            {
                RequestHandoffCentering(true);

                // The anchor snaps the floor to the nearest A at or below the sung note, so the floor
                // can no longer be picked to match wherever the attract pilot is holding the bird. The
                // pilot's ease to mid-screen only softens the resulting snap; SmoothDamp and
                // MaxVerticalSpeed absorb the rest. No teleport.
                if (!_anchor.TryCapture(sample.FrequencyHz, deltaTime))
                {
                    IsActive = false;
                    return;
                }

                RequestHandoffCentering(false);
            }

            // TODO: AdaptiveRecenterer is deliberately not wired in. Drifting the floor would move it
            // off an A, and the per-pipe note letters are derived from the floor, so every label would
            // be wrong. Re-enable only if the labels stop depending on the floor being an A.

            _height = PitchMath.ClampToOctaveHeight(midi, _anchor.FloorMidi, _config.OctaveWidthSemitones);
            IsActive = true;
        }

        private void RequestHandoffCentering(bool centering)
        {
            if (_attractPilot != null) _attractPilot.SetHandoffCentering(centering);
        }
    }
}
