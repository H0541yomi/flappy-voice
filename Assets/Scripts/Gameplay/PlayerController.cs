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

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite _idleSprite;
        [SerializeField] private Sprite _singSprite;
        [SerializeField] private Sprite _deadSprite;
        // Same silhouette as the dead pose with the colour blown out, so alternating the two is a
        // real white flash rather than a blink. A tint cannot brighten a sprite - the default
        // sprite shader multiplies - so the flash has to come from a second image.
        [SerializeField] private Sprite _flashSprite;
        [SerializeField] private float _flashesPerSec = 9f;
        // The voice gate drops out between syllables and on consonants. Swapping the sprite on
        // every one of those makes the bird flicker, so the singing pose is held briefly past the
        // end of the note rather than tracking the gate exactly.
        [SerializeField] private float _singPoseHoldSec = 0.12f;

        private Rigidbody2D _body;
        private Collider2D _collider;
        private Sprite _renderedPose;
        private float _lastVoicedTime;
        private LivesManager _lives;
        private Pipe _invinciblePastPipe;
        private float _invincibleEarliestEnd;
        private float _invincibleDeadline;

        // Struck a pipe, still has lives, and is flying through the pipe it hit. Nothing can kill
        // the bird until it is clear of that pipe - and never for less than MinInvincibleSec, so
        // the hit reads as a hit.
        public bool IsInvincible { get; private set; }
        private float _currentY;
        private float _dampVelocity;

        public float CurrentHeight01 => _config == null
            ? 0.5f
            : Mathf.Clamp01(Mathf.InverseLerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, _currentY));

        // Half the WIDTH of the body. Invincibility has to outlast the whole overlap with the
        // pipe that was hit, and the bird's trailing edge is this far behind its centre.
        private float BodyHalfWidthUnits => _collider != null ? _collider.bounds.extents.x : 0.36f;

        // Half the height of the body that has to fit through a pipe gap. The tuner reads it to
        // work out which pitches actually clear the gap ahead, so it has to be the collider's own
        // size rather than a number written down twice.
        public float BodyRadiusUnits => _collider != null ? _collider.bounds.extents.y : 0.36f;

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
            IsInvincible = false;

            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
        }

        // Idle, singing, or dead. Assigned only on a change: this runs every frame and the
        // renderer does real work when the sprite is set.
        private void UpdatePose()
        {
            if (_renderer == null)
            {
                return;
            }

            Sprite pose = _idleSprite;

            if (_state != null && _state.State == GameState.GameOver)
            {
                pose = _deadSprite != null ? _deadSprite : _idleSprite;
            }
            else if (IsInvincible)
            {
                // Alternates dead/white for the whole invincible window, so "I am hurt" and "I
                // cannot be hurt again yet" are the same signal.
                bool white = _flashSprite != null
                    && ((int)(Time.time * _flashesPerSec * 2f) & 1) == 1;
                pose = white ? _flashSprite : (_deadSprite != null ? _deadSprite : _idleSprite);
            }
            else
            {
                if (_voice != null && _voice.IsActive)
                {
                    _lastVoicedTime = Time.time;
                }

                if (_singSprite != null && Time.time - _lastVoicedTime <= _singPoseHoldSec)
                {
                    pose = _singSprite;
                }
            }

            if (pose == null || ReferenceEquals(pose, _renderedPose))
            {
                return;
            }

            _renderedPose = pose;
            _renderer.sprite = pose;
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

        public void SetLivesManager(LivesManager lives)
        {
            _lives = lives;
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

        private void Update()
        {
            UpdateInvincibility();
            UpdatePose();
        }

        private void UpdateInvincibility()
        {
            if (!IsInvincible)
            {
                return;
            }

            if (Time.time >= _invincibleDeadline)
            {
                EndInvincibility();
                return;
            }

            if (Time.time < _invincibleEarliestEnd)
            {
                return;
            }

            // Out the other side of the pipe that was hit. Measured against the bird's trailing
            // edge, not its centre: with the centre, the pipe's wall is still overlapping the back
            // half of the collider when invincibility ends, OnCollisionStay2D fires again on the
            // same pipe, and the bird burns every remaining life on one crash.
            bool crossed = _invinciblePastPipe == null
                || !_invinciblePastPipe.gameObject.activeInHierarchy
                || _invinciblePastPipe.X + (_invinciblePastPipe.Width * 0.5f)
                    < transform.position.x - BodyHalfWidthUnits;

            if (crossed)
            {
                EndInvincibility();
            }
        }

        private void EndInvincibility()
        {
            IsInvincible = false;
            _invinciblePastPipe = null;
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
            if (_state == null || _state.State != GameState.Playing || IsInvincible)
            {
                return;
            }

            Pipe pipe = collision.collider.GetComponentInParent<Pipe>();
            if (pipe == null)
            {
                return;
            }

            // A pipe you crashed into does not also pay out. Claiming the score zone here is what
            // stops it, since the bird still flies through the gap on its way past - and it is
            // what takes the gap's note letter down, whether the hit was survivable or not.
            pipe.HasScored = true;

            if (_lives != null && _lives.TryConsumeLife())
            {
                IsInvincible = true;
                _invinciblePastPipe = pipe;
                _invincibleEarliestEnd = Time.time
                    + (_config != null ? _config.MinInvincibleSec : 0.75f);
                _invincibleDeadline = Time.time
                    + (_config != null ? _config.MaxInvincibleSec : 4f);
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
