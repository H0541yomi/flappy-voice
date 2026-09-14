using FlappyVoice.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    // The row of hearts in the bottom-right corner: one per life the run starts with, filled while
    // it is still there. Only the sprite swaps — the row is built once, because the count cannot
    // change mid-run and rebuilding it would fight the layout.
    public sealed class LivesUI : MonoBehaviour
    {
        [SerializeField] private LivesManager livesManager;
        [SerializeField] private GameStateManager stateManager;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image[] hearts;
        [SerializeField] private Sprite fullSprite;
        [SerializeField] private Sprite emptySprite;

        private int renderedLives = int.MinValue;
        private bool subscribed;

        public void Configure(LivesManager lives, GameStateManager state)
        {
            Unsubscribe();
            livesManager = lives;
            stateManager = state;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            Render(livesManager != null ? livesManager.Lives : 0);
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        private void OnEnable()
        {
            Subscribe();
            Render(livesManager != null ? livesManager.Lives : 0);
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }
            if (livesManager != null)
            {
                livesManager.OnLivesChanged += Render;
            }
            if (stateManager != null)
            {
                stateManager.OnStateChanged += ApplyState;
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }
            if (livesManager != null)
            {
                livesManager.OnLivesChanged -= Render;
            }
            if (stateManager != null)
            {
                stateManager.OnStateChanged -= ApplyState;
            }
            subscribed = false;
        }

        private void Render(int lives)
        {
            if (lives == renderedLives || hearts == null)
            {
                return;
            }
            renderedLives = lives;

            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null)
                {
                    continue;
                }
                hearts[i].sprite = i < lives ? fullSprite : emptySprite;
            }
        }

        // Hidden outside a run: in attract the sign owns the screen and a full row of hearts reads
        // as a score, and on the end screen the card covers this corner anyway.
        private void ApplyState(GameState state)
        {
            if (group == null)
            {
                return;
            }
            group.alpha = state == GameState.Playing ? 1f : 0f;
        }
    }
}
