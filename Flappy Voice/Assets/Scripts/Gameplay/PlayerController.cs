using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        private GameConfig _config;
        private GameStateManager _state;
        private VoiceHeightSource _voice;
        private AttractPilot _attract;
        private ScoreManager _score;

        private Rigidbody2D _body;
        private float _currentY;
        private float _dampVelocity;
        private float _flapPhase;

        public float CurrentHeight01 => _config == null
            ? 0.5f
            : Mathf.Clamp01(Mathf.InverseLerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, _currentY));

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.simulated = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Pipe sections are static colliders; without full kinematic contacts a kinematic
            // body never raises collision events against them and death never fires.
            _body.useFullKinematicContacts = true;

            if (GetComponent<Collider2D>() == null)
            {
                CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.35f;
            }

            _currentY = transform.position.y;
        }

        public void Configure(GameConfig config, GameStateManager state, VoiceHeightSource voice, AttractPilot attract)
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }

            _config = config;
            _state = state;
            _voice = voice;
            _attract = attract;

            if (_attract != null)
            {
                _attract.SetCharacter(transform);
            }

            if (_state != null)
            {
                _state.OnStateChanged += HandleStateChanged;
            }
        }

        public void SetScoreManager(ScoreManager score)
        {
            _score = score;
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }
        }

        public void ResetToCenter()
        {
            if (_config == null)
            {
                return;
            }

            _currentY = (_config.PlayfieldMinY + _config.PlayfieldMaxY) * 0.5f;
            _dampVelocity = 0f;
            _flapPhase = 0f;
            ApplyPosition(_currentY);
        }

        private void HandleStateChanged(GameState state)
        {
            // Attract -> Playing must carry the smoothed position and damp velocity over untouched;
            // only a return to attract is allowed to re-seed the character.
            if (state != GameState.Attract)
            {
                return;
            }

            if (_voice != null)
            {
                _voice.ResetForNewRun();
            }

            ResetToCenter();
        }

        private void FixedUpdate()
        {
            if (_config == null)
            {
                return;
            }

            GameState state = _state != null ? _state.State : GameState.Attract;

            if (state == GameState.Attract && _state != null && _voice != null && _voice.IsAnchored)
            {
                _state.StartRun();
                state = _state.State;
            }

            if (state == GameState.GameOver)
            {
                return;
            }

            IHeightSource source = state == GameState.Playing ? (IHeightSource)_voice : _attract;
            float target01 = source != null ? Mathf.Clamp01(source.TargetHeight01) : 0.5f;
            float targetY = Mathf.Lerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, target01);

            float deltaTime = Time.fixedDeltaTime;
            float maxSpeed = Mathf.Max(0.01f, _config.MaxVerticalSpeed);

            float y = Mathf.SmoothDamp(_currentY, targetY, ref _dampVelocity,
                _config.HeightSmoothTimeSec, maxSpeed, deltaTime);

            float maxStep = maxSpeed * deltaTime;
            y = Mathf.Clamp(y, _currentY - maxStep, _currentY + maxStep);
            y = Mathf.Clamp(y, _config.PlayfieldMinY, _config.PlayfieldMaxY);

            _currentY = y;

            // The flap is added after SmoothDamp and after the MaxVerticalSpeed step clamp, both
            // of which govern the pitch-driven position only. Folding it in here keeps the bob at
            // full amplitude (its own ~5.5 u/s peak would otherwise be eaten by the speed budget)
            // while still moving the collider, so flapping up into a pipe kills the player.
            _flapPhase += Mathf.PI * 2f * Mathf.Max(0f, _config.FlapCyclesPerSec) * deltaTime;
            if (_flapPhase > Mathf.PI * 2f)
            {
                _flapPhase -= Mathf.PI * 2f;
            }

            float flapOffset = Mathf.Sin(_flapPhase) * _config.FlapAmplitudeUnits;
            float renderedY = Mathf.Clamp(y + flapOffset, _config.PlayfieldMinY, _config.PlayfieldMaxY);

            ApplyPosition(renderedY);
        }

        private void ApplyPosition(float y)
        {
            if (_body == null)
            {
                Vector3 p = transform.position;
                p.y = y;
                transform.position = p;
                return;
            }

            Vector2 target = _body.position;
            target.y = y;
            _body.MovePosition(target);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_state == null || _state.State != GameState.Playing)
            {
                return;
            }

            Pipe pipe = other.GetComponentInParent<Pipe>();
            if (pipe == null || pipe.HasScored)
            {
                return;
            }

            pipe.HasScored = true;

            if (_score != null)
            {
                _score.AddPoint();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_state == null || _state.State != GameState.Playing)
            {
                return;
            }

            if (collision.collider.GetComponentInParent<Pipe>() == null)
            {
                return;
            }

            if (_score != null)
            {
                _score.CommitBestScore();
            }

            _state.EndRun();
        }
    }
}
