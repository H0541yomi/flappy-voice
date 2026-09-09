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

        private readonly Queue<Pipe> _pool = new Queue<Pipe>();
        private readonly List<Pipe> _active = new List<Pipe>();

        private const string UnanchoredNoteLabel = "?";

        private GameConfig _config;
        private GameStateManager _state;
        private VoiceHeightSource _voice;
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

        // Note letters are derived from the anchored floor, which does not exist until the player
        // has sung. Every pipe already on screen has to be relabelled the moment it does.
        public void SetVoiceSource(VoiceHeightSource voice)
        {
            if (_voice != null)
            {
                _voice.OnAnchorChanged -= RelabelActivePipes;
            }

            _voice = voice;

            if (_voice != null)
            {
                _voice.OnAnchorChanged += RelabelActivePipes;
            }

            RelabelActivePipes();
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }

            if (_voice != null)
            {
                _voice.OnAnchorChanged -= RelabelActivePipes;
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
            CurrentGapSize = _config.PipeGapSize;
            _currentInterval = Mathf.Max(0.05f, _config.SpawnIntervalSec);
            _lastNoteOffset = -1;

            // first pipe appears immediately so attract mode always has a gap to fly toward
            _spawnTimer = _currentInterval;
        }

        public bool TryGetNextGapAhead(float x, out float gapCenterY)
        {
            gapCenterY = 0f;
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
            CurrentGapSize = Mathf.Lerp(_config.PipeGapSize, _config.MinPipeGapSize, difficulty);
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
            pipe.SetNote(noteOffset, NoteLabelForOffset(noteOffset));

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
            float half = (_pipePrefab != null ? _pipePrefab.VisualWidth : 1.5f) * 0.5f;
            return half + Mathf.Max(0f, _config.PipeEdgeMarginUnits);
        }

        private string NoteLabelForOffset(int noteOffset)
        {
            if (_voice == null || !_voice.IsAnchored)
            {
                return UnanchoredNoteLabel;
            }

            return PitchMath.NoteNameForMidi(PitchMath.RoundToSemitone(_voice.FloorMidi) + noteOffset);
        }

        private void RelabelActivePipes()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                Pipe pipe = _active[i];
                pipe.SetNote(pipe.NoteOffset, NoteLabelForOffset(pipe.NoteOffset));
            }
        }

        // The gap centre is the exact playfield Y the note maps the character to, so a pipe is
        // threaded by singing its letter and the gap size alone supplies the margin for error.
        private float GapCenterYForOffset(int noteOffset)
        {
            float height = PitchMath.HeightForOffset(noteOffset, _config.OctaveWidthSemitones);
            return Mathf.Lerp(_config.PlayfieldMinY, _config.PlayfieldMaxY, height);
        }

        private int PickNoteOffset()
        {
            int count = Mathf.Max(1, _config.OctaveWidthSemitones + 1);

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
            }
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
