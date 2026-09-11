using System.Collections.Generic;
using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class PipeSpawner : MonoBehaviour
    {
        [SerializeField] private Pipe _pipePrefab;
        [SerializeField] private Camera _viewCamera;
        [SerializeField] private int _poolSize = 8;
        [SerializeField] private int _maxNoteStepSemitones = 7;
        [SerializeField] private float _reachSafetyFactor = 0.55f;
        // Extra room beyond the bird's own radius when clearing pipes at the start of a run.
        [SerializeField] private float _runStartClearanceUnits = 0.4f;

        private readonly Queue<Pipe> _pool = new Queue<Pipe>();
        private readonly List<Pipe> _active = new List<Pipe>();

        private const float FallbackPlayerRadius = 0.42f;

        private GameConfig _config;
        private GameStateManager _state;
        private PlayerController _player;
        private float _spawnTimer;
        private float _currentInterval;
        private int _lastNoteOffset = -1;

        public float CurrentSpeed { get; private set; }
        public float CurrentGapSize { get; private set; }
        public IReadOnlyList<Pipe> ActivePipes => _active;

        private void Awake()
        {
            if (_viewCamera == null) _viewCamera = Camera.main;
        }

        public void Configure(GameConfig config, GameStateManager state)
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }

            _config = config;
            _state = state;

            if (_state != null)
            {
                _state.OnStateChanged += HandleStateChanged;
            }

            if (_config == null)
            {
                return;
            }

            EnsurePool();
            ResetSpawner();
        }

        // The bird's X never moves, but its collider does have width: the spawner needs it to know
        // which pipes are on top of the bird when a run starts.
        public void SetPlayer(PlayerController player)
        {
            _player = player;
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }
        }

        public void ResetSpawner()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Recycle(i);
            }

            if (_config == null)
            {
                return;
            }

            CurrentSpeed = _config.PipeSpeed;
            CurrentGapSize = _config.PipeGapSizeAtDifficulty(0f);
            _currentInterval = Mathf.Max(0.05f, _config.SpawnIntervalSec);
            _lastNoteOffset = -1;

            // first pipe appears immediately so attract mode always has a gap to fly toward
            _spawnTimer = _currentInterval;
        }

        public bool TryGetNextGapAhead(float x, out float gapCenterY)
        {
            return TryGetNextGapAhead(x, out gapCenterY, out _);
        }

        // The gap's size comes back with its centre because the tuner has to know how much room
        // there actually is: the ramp shrinks the opening as a run goes on, and the band of notes
        // that clears it shrinks with it.
        public bool TryGetNextGapAhead(float x, out float gapCenterY, out float gapSize)
        {
            gapCenterY = 0f;
            gapSize = 0f;
            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _active.Count; i++)
            {
                Pipe pipe = _active[i];
                float pipeX = pipe.X;
                if (pipeX > x && pipeX < nearest)
                {
                    nearest = pipeX;
                    gapCenterY = pipe.GapCenterY;
                    gapSize = pipe.GapSize;
                    found = true;
                }
            }

            return found;
        }

        private void Update()
        {
            if (_config == null)
            {
                return;
            }

            if (_state != null && _state.State == GameState.GameOver)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateDifficulty();

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Pipe pipe = _active[i];
                pipe.Move(CurrentSpeed, deltaTime);
                if (pipe.X < DespawnX())
                {
                    Recycle(i);
                }
            }

            _spawnTimer += deltaTime;
            if (_spawnTimer >= _currentInterval)
            {
                _spawnTimer -= _currentInterval;
                Spawn();
            }
        }

        private void UpdateDifficulty()
        {
            float elapsed = _state != null ? _state.RunElapsedSec : 0f;
            float duration = Mathf.Max(0.01f, _config.DifficultyRampDurationSec);
            float t = Mathf.Clamp01(elapsed / duration);

            AnimationCurve curve = _config.DifficultyRampCurve;
            float difficulty = curve != null ? Mathf.Clamp01(curve.Evaluate(t)) : t;

            CurrentSpeed = Mathf.Lerp(_config.PipeSpeed, _config.MaxPipeSpeed, difficulty);
            CurrentGapSize = _config.PipeGapSizeAtDifficulty(difficulty);
            _currentInterval = Mathf.Max(0.05f,
                Mathf.Lerp(_config.SpawnIntervalSec, _config.MinSpawnIntervalSec, difficulty));
        }

        private void Spawn()
        {
            Pipe pipe = Rent();
            if (pipe == null)
            {
                return;
            }

            int noteOffset = PickNoteOffset();
            float gapCenterY = GapCenterYForOffset(noteOffset);

            pipe.transform.position = new Vector3(SpawnX(), 0f, 0f);
            pipe.gameObject.SetActive(true);
            pipe.Setup(gapCenterY, CurrentGapSize, _config.PlayfieldMinY, _config.PlayfieldMaxY);
            pipe.SetNoteOffset(noteOffset);

            _active.Add(pipe);
            _lastNoteOffset = noteOffset;
        }

        // Spawn and despawn are pinned to the camera frustum, not to fixed X values: at a phone's
        // portrait aspect a fixed +-5 sits well outside the view, and in a wide editor Game view it
        // sits well inside it, which is exactly where pipes were seen popping in and vanishing.
        private float SpawnX()
        {
            return ViewCenterX() + ViewHalfWidth() + EdgeClearance();
        }

        private float DespawnX()
        {
            return ViewCenterX() - ViewHalfWidth() - EdgeClearance();
        }

        private float ViewCenterX()
        {
            return _viewCamera != null ? _viewCamera.transform.position.x : 0f;
        }

        private float ViewHalfWidth()
        {
            if (_viewCamera == null || !_viewCamera.orthographic)
            {
                // No usable camera: fall back to the authored offsets, whose magnitude is the only
                // half-width information the config carries.
                return Mathf.Max(Mathf.Abs(_config.PipeSpawnXOffset), Mathf.Abs(_config.PipeDespawnX));
            }

            return _viewCamera.orthographicSize * _viewCamera.aspect;
        }

        private float EdgeClearance()
        {
            float half = (_pipePrefab != null ? _pipePrefab.Width : 1.4f) * 0.5f;
            return half + Mathf.Max(0f, _config.PipeEdgeMarginUnits);
        }

        // noteOffset is the LOWER note of the pair the gap spans, so the centre lands on the
        // boundary between that note and the next one up. Either of the two letters on the chip
        // threads the pipe, and each gets the same margin either side.
        private float GapCenterYForOffset(int noteOffset)
        {
            float height = PitchMath.HeightForNotePair(noteOffset, _config.OctaveWidthSemitones);
            return Mathf.Lerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, height);
        }

        private int PickNoteOffset()
        {
            // One pair per adjacent note couple: offsets 0..width-1 pair note i with note i+1, and
            // the topmost note is the upper half of the last pair rather than a pair of its own.
            int count = Mathf.Max(1, _config.OctaveWidthSemitones);

            if (count < 2)
            {
                return 0;
            }

            if (_lastNoteOffset < 0 || _lastNoteOffset >= count)
            {
                return Random.Range(0, count);
            }

            int step = MaxStepSemitones(count);
            int lo = Mathf.Max(0, _lastNoteOffset - step);
            int hi = Mathf.Min(count - 1, _lastNoteOffset + step);
            int candidates = hi - lo;

            if (candidates < 1)
            {
                lo = 0;
                candidates = count - 1;
            }

            // draw over the window minus the previous offset, then shift past it: uniform, no
            // allocation, and a repeat is unrepresentable rather than merely unlikely
            int pick = lo + Random.Range(0, candidates);
            if (pick >= _lastNoteOffset)
            {
                pick++;
            }

            return pick;
        }

        private int MaxStepSemitones(int count)
        {
            int cap = Mathf.Clamp(_maxNoteStepSemitones, 1, count - 1);

            float span = _config.PlayfieldMaxY - _config.PlayfieldMinY;
            int width = Mathf.Max(1, _config.OctaveWidthSemitones);
            float unitsPerSemitone = span / width;

            if (unitsPerSemitone <= 0f)
            {
                return cap;
            }

            float reachUnits = _config.MaxVerticalSpeed * _currentInterval * _reachSafetyFactor;
            int reachSemitones = Mathf.Max(1, Mathf.FloorToInt(reachUnits / unitsPerSemitone));
            return Mathf.Min(cap, reachSemitones);
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Attract)
            {
                ResetSpawner();
                return;
            }

            if (state == GameState.Playing)
            {
                ClearPipesOnPlayer();
            }
        }

        // Attract mode eases the bird toward mid-screen as soon as it hears a voice, so by the time
        // the sustained note anchors the range the bird can already be inside the pipe that was on
        // its way in. That overlap began while death was still switched off, and a collider that is
        // already overlapping raises no fresh OnCollisionEnter2D - which is how a player could sing
        // and then sail straight through the first pipe. Recycling whatever the bird is standing in
        // at the handoff removes the overlap instead of leaving it to be ignored.
        private void ClearPipesOnPlayer()
        {
            float playerX = _player != null ? _player.transform.position.x : 0f;
            float clearance = PlayerRadius() + Mathf.Max(0f, _runStartClearanceUnits)
                + (_pipePrefab != null ? _pipePrefab.Width : 1.4f) * 0.5f;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (Mathf.Abs(_active[i].X - playerX) <= clearance)
                {
                    Recycle(i);
                }
            }
        }

        private float PlayerRadius()
        {
            if (_player == null)
            {
                return FallbackPlayerRadius;
            }

            Collider2D collider = _player.GetComponent<Collider2D>();
            if (collider == null)
            {
                return FallbackPlayerRadius;
            }

            Bounds bounds = collider.bounds;
            return Mathf.Max(bounds.extents.x, FallbackPlayerRadius);
        }

        private void EnsurePool()
        {
            int missing = _poolSize - (_pool.Count + _active.Count);
            for (int i = 0; i < missing; i++)
            {
                _pool.Enqueue(CreatePipe());
            }
        }

        private Pipe Rent()
        {
            Pipe pipe = _pool.Count > 0 ? _pool.Dequeue() : CreatePipe();
            return pipe;
        }

        private Pipe CreatePipe()
        {
            Pipe pipe;

            if (_pipePrefab != null)
            {
                pipe = Instantiate(_pipePrefab, transform);
            }
            else
            {
                GameObject go = new GameObject("Pipe");
                go.transform.SetParent(transform, false);
                pipe = go.AddComponent<Pipe>();
            }

            pipe.gameObject.SetActive(false);
            return pipe;
        }

        private void Recycle(int index)
        {
            Pipe pipe = _active[index];
            _active.RemoveAt(index);
            pipe.HasScored = false;
            pipe.gameObject.SetActive(false);
            _pool.Enqueue(pipe);
        }
    }
}
