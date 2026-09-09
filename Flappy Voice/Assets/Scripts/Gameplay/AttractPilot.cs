using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class AttractPilot : MonoBehaviour, IHeightSource
    {
        [SerializeField] private Transform _character;
        [SerializeField] private float _easeTimeSec = 0.25f;
        [SerializeField] private float _gapHoldMargin = 1.2f;

        private GameConfig _config;
        private PipeSpawner _spawner;
        private float _height = 0.5f;
        private float _velocity;

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

            float target = 0.5f;

            if (_spawner != null)
            {
                float queryX = (_character != null ? _character.position.x : 0f) - _gapHoldMargin;
                if (_spawner.TryGetNextGapAhead(queryX, out float gapCenterY))
                {
                    target = Mathf.Clamp01(
                        Mathf.InverseLerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, gapCenterY));
                }
            }

            _height = Mathf.SmoothDamp(_height, target, ref _velocity, _easeTimeSec, Mathf.Infinity, Time.deltaTime);
        }
    }
}
