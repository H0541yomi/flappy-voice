using FlappyVoice.Gameplay;
using FlappyVoice.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    /// <summary>
    /// The player's way out of the game, shown in the screen's top-right corner whenever a panel
    /// is up and hidden while a run is in progress. Tapping it asks the embedding host to close
    /// the game (see <see cref="HostBridge"/>).
    /// </summary>
    // Corner of the SCREEN, not of the panel it accompanies: the three panels are three different
    // sizes in three different places, and a button that moved with them would be a different
    // control each time. SceneBuilder therefore parents it to the canvas rather than to a sign,
    // and builds it last so it stays on top of the consent flow's blocking dim.
    //
    // Visibility keys off the game state rather than off the panels themselves. Attract always has
    // exactly one sign out - the consent parchment, the microphone notice, or the start sign - and
    // GameOver always has the end screen, so "not Playing" IS "a panel is out", and it needs one
    // subscription instead of three.
    public sealed class QuitButtonUI : MonoBehaviour
    {
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private GameObject root;
        [SerializeField] private Button quitButton;

        private bool subscribed;

        /// <summary>
        /// Replays the edit-time wiring at runtime: the state manager lives in a non-serialized
        /// field here, so the scene save discards it and GameBootstrap hands it back.
        /// </summary>
        public void Configure(GameStateManager state)
        {
            Unsubscribe();
            stateManager = state;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        private void OnEnable()
        {
            Subscribe();
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
            if (stateManager != null)
            {
                stateManager.OnStateChanged += ApplyState;
            }
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuit);
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }
            if (stateManager != null)
            {
                stateManager.OnStateChanged -= ApplyState;
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuit);
            }
            subscribed = false;
        }

        private void ApplyState(GameState state)
        {
            bool visible = state != GameState.Playing;
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }

        private void OnQuit()
        {
            HostBridge.RequestQuit();
        }
    }
}
