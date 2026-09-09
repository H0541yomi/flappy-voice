using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class AttractPilot : MonoBehaviour, IHeightSource
    {
        private const float DefaultHeight = 0.5f;

        [SerializeField] private Transform _character;
        [SerializeField] private float _easeTimeSec = 0.25f;
        [SerializeField] private float _gapHoldMargin = 1.2f;
        [SerializeField] private float _handoffCenterHeight = 0.5f;
        [SerializeField] private float _handoffCenterTimeSec = 0.2f;

        private GameConfig _config;
        private PipeSpawner _spawner;
        private float _height = DefaultHeight;
        private float _velocity;
        private bool _centeringForHandoff;
        private float _centerBlend;

        public float TargetHeight01 => _height;
        public bool IsActive => _config != null;

        public void Configure(GameConfig config, PipeSpawner spawner)
        {
            _config = config;
            _spawner = spawner;

            if (_character == null)
            {
                _character = transform;
            }
        }

        public void SetCharacter(Transform character)
        {
            if (character != null)
            {
                _character = character;
            }
        }

        // Voice is present but the octave floor is not captured yet: ease toward mid-screen so the
        // jump when the floor snaps to an A is as small as possible.
        public void SetHandoffCentering(bool centering)
        {
            _centeringForHandoff = centering;
        }

        public void ResetState()
        {
            _height = DefaultHeight;
            _velocity = 0f;
            _centerBlend = 0f;
            _centeringForHandoff = false;
        }

        private void Awake()
        {
            if (_character == null)
            {
                _character = transform;
            }
        }

        private void Update()
        {
            if (_config == null)
            {
                return;
            }

            float target = DefaultHeight;

            if (_spawner != null)
            {
                float queryX = (_character != null ? _character.position.x : 0f) - _gapHoldMargin;
                if (_spawner.TryGetNextGapAhead(queryX, out float gapCenterY))
                {
                    target = Mathf.Clamp01(
                        Mathf.InverseLerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, gapCenterY));
                }
            }

            float blendStep = _handoffCenterTimeSec > 0f ? Time.deltaTime / _handoffCenterTimeSec : 1f;
            _centerBlend = Mathf.Clamp01(_centeringForHandoff ? _centerBlend + blendStep : _centerBlend - blendStep);
            if (_centerBlend > 0f)
            {
                target = Mathf.Lerp(target, Mathf.Clamp01(_handoffCenterHeight), _centerBlend);
            }

            _height = Mathf.SmoothDamp(_height, target, ref _velocity, _easeTimeSec, Mathf.Infinity, Time.deltaTime);
        }
    }
}
