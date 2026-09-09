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
        private Collider2D _collider;
        private float _currentY;
        private float _dampVelocity;

        public float CurrentHeight01 => _config == null
            ? 0.5f
            : Mathf.Clamp01(Mathf.InverseLerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, _currentY));

        // Half the height of the body that has to fit through a pipe gap. The tuner reads it to
        // work out which pitches actually clear the gap ahead, so it has to be the collider's own
        // size rather than a number written down twice.
        public float BodyRadiusUnits => _collider != null ? _collider.bounds.extents.y : 0.42f;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.simulated = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Pipe sections are static colliders; without full kinematic contacts a kinematic
            // body never raises collision events against them and death never fires.
            _body.useFullKinematicContacts = true;

            _collider = GetComponent<Collider2D>();
            if (_collider == null)
            {
                CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
                circle.radius = 0.35f;
                _collider = circle;
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

            ApplyPosition(y);
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
            HandlePipeContact(collision);
        }

        // Enter fires once, on the frame the overlap begins. An overlap that began while the run
        // had not started yet - death is off in attract mode - therefore never produces an Enter
        // once it does start, and the pipe would slide harmlessly through the bird. Stay closes
        // that hole; the spawner also clears pipes sitting on the bird at the handoff, so this
        // fires for real mid-run contact rather than for a start the player never controlled.
        private void OnCollisionStay2D(Collision2D collision)
        {
            HandlePipeContact(collision);
        }

        private void HandlePipeContact(Collision2D collision)
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
