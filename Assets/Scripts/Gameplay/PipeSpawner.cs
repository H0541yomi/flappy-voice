using System.Collections.Generic;
using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class PipeSpawner : MonoBehaviour
    {
        [SerializeField] private Pipe _pipePrefab;
        [SerializeField] private Camera _viewCamera;
        // Only the floor is wanted, to name the note each gap sits on. Serialized and auto-wired
        // like the camera, so the letters survive a scene save on their own.
        [SerializeField] private VoiceHeightSource _voiceSource;
        [SerializeField] private int _poolSize = 8;
        [SerializeField] private int _maxNoteStepSemitones = 7;
        [SerializeField] private float _reachSafetyFactor = 0.55f;
        // Extra room beyond the bird's own radius when clearing pipes at the start of a run.
        [SerializeField] private float _runStartClearanceUnits = 0.4f;

        private readonly Queue<Pipe> _pool = new Queue<Pipe>();
        private readonly List<Pipe> _active = new List<Pipe>();

        // Only reached before Configure has run or if the player has no collider; the real
        // radius is the collider's, and GameConfig is the single source for what that is.
        private const float FallbackPlayerRadius = 0.36f;

        private GameConfig _config;
        private GameStateManager _state;
        private ScoreManager _score;
        private PlayerController _player;
        private float _spawnDistance;
        private float _currentInterval;
        private int _lastNoteOffset = -1;

        public float CurrentSpeed { get; private set; }
        public float CurrentGapSize { get; private set; }
        public IReadOnlyList<Pipe> ActivePipes => _active;

        private void Awake()
        {
            if (_viewCamera == null) _viewCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (_voiceSource != null) _voiceSource.OnAnchorChanged += RelabelActivePipes;
        }

        private void OnDisable()
        {
            if (_voiceSource != null) _voiceSource.OnAnchorChanged -= RelabelActivePipes;
        }

        public void Configure(GameConfig config, GameStateManager state, ScoreManager score)
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }

            _config = config;
            _state = state;
            _score = score;

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

            CurrentSpeed = _config.PipeSpeedAtDifficulty(0f);
            CurrentGapSize = _config.PipeGapSizeAtDifficulty(0f);
            _currentInterval = Mathf.Max(0.05f, _config.SpawnIntervalSecAtSpeed(CurrentSpeed));
            _lastNoteOffset = -1;

            // first pipe appears immediately so attract mode always has a gap to fly toward
            _spawnDistance = SpawnSpacing();
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

            _spawnDistance += CurrentSpeed * deltaTime;
            float spacing = SpawnSpacing();
            if (_spawnDistance >= spacing)
            {
                _spawnDistance -= spacing;
                Spawn();
            }
        }

        // Pipes passed, not seconds elapsed: the run gets harder because the player is doing
        // well, not because they are still alive. Spacing stays put - what ramps is how fast the
        // pipes come at you and how little room there is around the note.
        private void UpdateDifficulty()
        {
            int passed = _score != null ? _score.Score : 0;
            float difficulty = _config.DifficultyForPipesPassed(passed);

            CurrentSpeed = _config.PipeSpeedAtDifficulty(difficulty);
            CurrentGapSize = _config.PipeGapSizeAtDifficulty(difficulty);
            _currentInterval = Mathf.Max(0.05f, _config.SpawnIntervalSecAtSpeed(CurrentSpeed));
        }

        // Pipes are paced by distance travelled, not by a timer: the speed ramps mid-run, and
        // accumulating time against an interval that is itself moving leaves each pipe a little
        // closer to the last one. Distance since the previous spawn is what "10.4 units apart"
        // actually means, at any speed and through any change of speed.
        private float SpawnSpacing()
        {
            return Mathf.Max(0.1f, _config.PipeSpacingUnits);
        }

        public float CurrentDifficulty01 =>
            _config != null ? _config.DifficultyForPipesPassed(_score != null ? _score.Score : 0) : 0f;

        private void Spawn()
        {
            Pipe pipe = Rent();
            if (pipe == null)
            {
                return;
            }

            Place(pipe, PickNoteOffset());
        }

        private void Place(Pipe pipe, int noteOffset)
        {
            float gapCenterY = GapCenterYForOffset(noteOffset);

            pipe.transform.position = new Vector3(SpawnX(), 0f, 0f);
            pipe.gameObject.SetActive(true);
            pipe.Setup(gapCenterY, CurrentGapSize, _config.PlayfieldMinY, _config.PlayfieldMaxY);
            pipe.SetNoteOffset(noteOffset);
            pipe.SetNoteLabel(NoteNameForOffset(noteOffset));

            _active.Add(pipe);
            _lastNoteOffset = noteOffset;
        }

        // The letter a gap is asking the player for. Offsets are semitones above the anchored
        // floor, so before the first note has anchored the range there is no letter to give: the
        // pipe is not on a note yet, it is on an offset from a floor nobody has set.
        private string NoteNameForOffset(int noteOffset)
        {
            if (_voiceSource == null || !_voiceSource.IsAnchored)
            {
                return string.Empty;
            }

            return PitchMath.NoteNameForOffset(_voiceSource.FloorMidi, noteOffset);
        }

        // The floor has moved - a run just anchored, or went back to attract - so every letter
        // already on screen names the wrong note. Never more than the pool's worth of pipes.
        private void RelabelActivePipes()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                _active[i].SetNoteLabel(NoteNameForOffset(_active[i].NoteOffset));
            }
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

        // noteOffset is the note the gap is centred ON, so the note gets the same margin above
        // it as below it and it is the only note of the range that threads the pipe.
        private float GapCenterYForOffset(int noteOffset)
        {
            float height = PitchMath.HeightForOffset(noteOffset, _config.OctaveWidthSemitones);
            return Mathf.Lerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, height);
        }

        private int PickNoteOffset()
        {
            // Offsets are semitones above the anchored floor, so a range `count` semitones wide
            // holds count + 1 notes: both ends are real, singable positions the bird can reach.
            int count = Mathf.Max(1, _config.OctaveWidthSemitones);
            int top = count;

            if (_lastNoteOffset < 0 || _lastNoteOffset > top)
            {
                return Random.Range(0, top + 1);
            }

            int step = MaxStepSemitones(count);
            int lo = Mathf.Max(0, _lastNoteOffset - step);
            int hi = Mathf.Min(top, _lastNoteOffset + step);
            int candidates = hi - lo;

            if (candidates < 1)
            {
                lo = 0;
                candidates = top;
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

            // _currentInterval shrinks as the speed ramps, so the window of notes the next gap
            // may sit on narrows with it: less time between pipes is less distance the bird can
            // sing its way across.
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

        private float PipeWidth()
        {
            return _pipePrefab != null ? _pipePrefab.Width : 1.4f;
        }

        private float ConfiguredRadius()
        {
            return _config != null ? _config.PlayerBodyRadiusUnits : FallbackPlayerRadius;
        }

        private float PlayerRadius()
        {
            if (_player == null)
            {
                return ConfiguredRadius();
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
