using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    // How many more times the bird may hit a pipe before the run is over. Separate from the
    // player so the HUD and the end screen can read it without reaching into collision code.
    public sealed class LivesManager : MonoBehaviour
    {
        private GameConfig _config;
        private GameStateManager _state;

        public int Lives { get; private set; }
        public int MaxLives => _config != null ? Mathf.Max(1, _config.PlayerLives) : 3;
        public bool HasLivesLeft => Lives > 0;

        public event System.Action<int> OnLivesChanged;

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

            ResetLives();
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }
        }

        public void ResetLives()
        {
            Lives = MaxLives;
            OnLivesChanged?.Invoke(Lives);
        }

        // True when the bird survives the hit and should go invincible; false when that was the
        // last life and the run is over.
        public bool TryConsumeLife()
        {
            if (Lives <= 0)
            {
                return false;
            }

            Lives--;
            OnLivesChanged?.Invoke(Lives);
            return Lives > 0;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                ResetLives();
            }
        }
    }
}
