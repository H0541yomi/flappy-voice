using FlappyVoice.Audio;
using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class VoiceHeightSource : MonoBehaviour, IHeightSource
    {
        private readonly OctaveAnchor _anchor = new OctaveAnchor();
        private readonly AdaptiveRecenterer _recenterer = new AdaptiveRecenterer();

        private GameConfig _config;
        private PitchTracker _tracker;
        private float _height = 0.5f;

        public OctaveAnchor Anchor => _anchor;
        public bool IsAnchored => _anchor.IsAnchored;
        public float TargetHeight01 => _height;
        public bool IsActive { get; private set; }

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
            IsActive = false;
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
                IsActive = false;
                return;
            }

            float deltaTime = Time.deltaTime;

            if (!_anchor.IsAnchored)
            {
                _anchor.TryCapture(sample.FrequencyHz, deltaTime);
                if (!_anchor.IsAnchored)
                {
                    IsActive = false;
                    return;
                }
            }

            float midi = PitchMath.HzToMidi(sample.FrequencyHz);
            float height = PitchMath.WrapToOctaveHeight(midi, _anchor.FloorMidi, _config.OctaveWidthSemitones);

            _anchor.SetFloorMidi(_recenterer.Update(height, _anchor.FloorMidi, deltaTime));

            _height = height;
            IsActive = true;
        }
    }
}
