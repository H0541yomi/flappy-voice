using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class GameStateManager : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;

        public GameState State { get; private set; } = GameState.Attract;
        public GameConfig Config => _config;
        public float RunElapsedSec { get; private set; }

        public event System.Action<GameState> OnStateChanged;

        public void Configure(GameConfig config)
        {
            if (config != null)
            {
                _config = config;
            }
        }

        private void Awake()
        {
            if (_config == null)
            {
                _config = GameConfig.CreateDefault();
            }
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
