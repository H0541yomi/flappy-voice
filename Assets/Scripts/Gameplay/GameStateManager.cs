using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class GameStateManager : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;

        private bool _ownsConfig;

        public GameState State { get; private set; } = GameState.Attract;
        public GameConfig Config => _config;
        public float RunElapsedSec { get; private set; }

        public event System.Action<GameState> OnStateChanged;

        public void Configure(GameConfig config)
        {
            if (config == null) return;

            DisposeOwnedConfig();
            _config = config;
        }

        private void Awake()
        {
            if (_config != null) return;

            _config = GameConfig.CreateDefault();
            _ownsConfig = true;
        }

        private void OnDestroy()
        {
            DisposeOwnedConfig();
        }

        // CreateDefault() returns a live ScriptableObject that nothing else owns; without this it
        // survives scene unload and leaks one instance per load.
        private void DisposeOwnedConfig()
        {
            if (!_ownsConfig || _config == null) return;

            _ownsConfig = false;
            GameConfig stale = _config;
            _config = null;

            if (Application.isPlaying) Destroy(stale);
            else DestroyImmediate(stale);
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                RunElapsedSec += Time.deltaTime;
            }
        }

        public void StartRun()
        {
            if (State != GameState.Attract)
            {
                return;
            }

            RunElapsedSec = 0f;
            Transition(GameState.Playing);
        }

        public void EndRun()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            Transition(GameState.GameOver);
        }

        public void RestartToAttract()
        {
            if (State == GameState.Attract)
            {
                return;
            }

            RunElapsedSec = 0f;
            Transition(GameState.Attract);
        }

        private void Transition(GameState next)
        {
            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
