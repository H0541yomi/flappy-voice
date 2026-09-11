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

        private GameConfig _config;
        private PipeSpawner _spawner;
        private float _height = DefaultHeight;
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

        // Height of the gap the pilot is currently flying at, if there is one to fly at. The anchor
        // reads this at capture time and places the first sung note here, which is what makes that
        // note the one that threads the first pipe - so the pilot must NOT be eased off the gap
        // while a note is being captured, and nothing here does that any more.
        public bool TryGetGapTargetHeight(out float height01)
        {
            height01 = DefaultHeight;

            if (_config == null || _spawner == null)
            {
                return false;
            }

            float queryX = (_character != null ? _character.position.x : 0f) - _gapHoldMargin;
            if (!_spawner.TryGetNextGapAhead(queryX, out float gapCenterY))
            {
                return false;
            }

            height01 = Mathf.Clamp01(Mathf.InverseLerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, gapCenterY));
            return true;
        }

        public void ResetState()
        {
            _height = DefaultHeight;
            _velocity = 0f;
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

            float target = TryGetGapTargetHeight(out float gapHeight) ? gapHeight : DefaultHeight;

            _height = Mathf.SmoothDamp(_height, target, ref _velocity, _easeTimeSec, Mathf.Infinity, Time.deltaTime);
        }
    }
}
