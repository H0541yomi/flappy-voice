using System.Collections.Generic;
using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class PipeSpawner : MonoBehaviour
    {
        [SerializeField] private Pipe _pipePrefab;
        [SerializeField] private int _poolSize = 8;
        [SerializeField] private float _gapEdgeMargin = 0.4f;
        [SerializeField] private float _reachSafetyFactor = 0.55f;

        private readonly Queue<Pipe> _pool = new Queue<Pipe>();
        private readonly List<Pipe> _active = new List<Pipe>();

        private GameConfig _config;
        private GameStateManager _state;
        private float _spawnTimer;
        private float _currentInterval;
        private float _lastGapCenterY;

        public float CurrentSpeed { get; private set; }
        public float CurrentGapSize { get; private set; }
        public IReadOnlyList<Pipe> ActivePipes => _active;

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
            CurrentGapSize = _config.PipeGapSize;
            _currentInterval = Mathf.Max(0.05f, _config.SpawnIntervalSec);
            _lastGapCenterY = (_config.PlayfieldMinY + _config.PlayfieldMaxY) * 0.5f;

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
                if (pipe.X < _config.PipeDespawnX)
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

            float gapCenterY = PickGapCenterY();

            pipe.transform.position = new Vector3(_config.PipeSpawnXOffset, 0f, 0f);
            pipe.gameObject.SetActive(true);
            pipe.Setup(gapCenterY, CurrentGapSize, _config.PlayfieldMinY, _config.PlayfieldMaxY);

            _active.Add(pipe);
            _lastGapCenterY = gapCenterY;
        }

        private float PickGapCenterY()
        {
            float halfGap = CurrentGapSize * 0.5f;
            float min = _config.PlayfieldMinY + halfGap + _gapEdgeMargin;
            float max = _config.PlayfieldMaxY - halfGap - _gapEdgeMargin;

            if (min > max)
            {
                float mid = (_config.PlayfieldMinY + _config.PlayfieldMaxY) * 0.5f;
                return mid;
            }

            // a gap the character cannot physically reach from the previous one is an unwinnable
            // sequence for both the attract pilot and a real singer, so cap the vertical step
            float reach = _config.MaxVerticalSpeed * _currentInterval * _reachSafetyFactor;
            float lo = Mathf.Max(min, _lastGapCenterY - reach);
            float hi = Mathf.Min(max, _lastGapCenterY + reach);

            if (lo > hi)
            {
                return Mathf.Clamp(_lastGapCenterY, min, max);
            }

            return Random.Range(lo, hi);
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
