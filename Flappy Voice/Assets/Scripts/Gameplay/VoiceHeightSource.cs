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
        private readonly AdaptiveRecenterer _recenterer = new AdaptiveRecenterer();

        private GameConfig _config;
        private PitchTracker _tracker;
        private float _height = 0.5f;
        private float _clampMinMidi;
        private float _clampMaxMidi;

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

            _clampMinMidi = PitchMath.HzToMidi(_config.VocalRangeClampMinHz);
            _clampMaxMidi = PitchMath.HzToMidi(_config.VocalRangeClampMaxHz);
            if (_clampMaxMidi < _clampMinMidi)
            {
                float swap = _clampMinMidi;
                _clampMinMidi = _clampMaxMidi;
                _clampMaxMidi = swap;
            }

            _anchor.Configure(
                _config.AnchorCaptureWindowMs / 1000f,
                _config.AnchorStabilityToleranceSemitones,
                _config.VocalRangeClampMinHz,
                _config.VocalRangeClampMaxHz);

            _recenterer.Configure(
                _config.RecenterWindowSec,
                _config.RecenterDriftRatePerSec,
                _config.RecenterEdgeThreshold,
                _config.OctaveWidthSemitones);
        }

        public void ResetForNewRun()
        {
            _anchor.Reset();
            _recenterer.Reset();
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

                if (!_anchor.TryCapture(sample.FrequencyHz, deltaTime))
                {
                    IsActive = false;
                    return;
                }

                AnchorAtCurrentHeight(midi);
                RequestHandoffCentering(false);
            }

            float height = PitchMath.WrapToOctaveHeight(midi, _anchor.FloorMidi, _config.OctaveWidthSemitones);

            _anchor.SetFloorMidi(_recenterer.Update(height, _anchor.FloorMidi, deltaTime));

            _height = height;
            IsActive = true;
        }

        private void RequestHandoffCentering(bool centering)
        {
            if (_attractPilot != null) _attractPilot.SetHandoffCentering(centering);
        }

        // The captured note must NOT map to height 0, or the player's own comfortable pitch lands on
        // the wrap seam and normal vibrato swings them the full height of the screen. Re-derive the
        // floor so the note maps to wherever the attract pilot currently holds the character (it has
        // been easing to mid-screen for the whole capture window), which keeps the handoff free of
        // both a teleport and a seam park.
        private void AnchorAtCurrentHeight(float sungMidi)
        {
            float width = _config.OctaveWidthSemitones;
            if (width <= 0f) return;

            float handoffHeight = _attractPilot != null
                ? Mathf.Clamp01(_attractPilot.TargetHeight01)
                : FallbackHandoffHeight;

            float floorMidi = sungMidi - handoffHeight * width;

            // Height is periodic in the floor with period `width`, so shifting by whole octaves keeps
            // the mapping identical while putting the floor back inside the cough/thump sanity range.
            if (floorMidi < _clampMinMidi && _clampMaxMidi - _clampMinMidi >= width)
            {
                floorMidi += width * Mathf.Ceil((_clampMinMidi - floorMidi) / width);
            }
            else if (floorMidi > _clampMaxMidi && _clampMaxMidi - _clampMinMidi >= width)
            {
                floorMidi -= width * Mathf.Ceil((floorMidi - _clampMaxMidi) / width);
            }

            _anchor.SetFloorMidi(floorMidi);
        }
    }
}
