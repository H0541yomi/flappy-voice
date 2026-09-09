using FlappyVoice.Gameplay;
using FlappyVoice.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    public sealed class EndScreenUI : MonoBehaviour
    {
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private ShareService shareService;

        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform scoreCardRoot;
        [SerializeField] private Camera uiCamera;
        [SerializeField] private TextMeshProUGUI finalScoreLabel;
        [SerializeField] private TextMeshProUGUI bestScoreLabel;
        [SerializeField] private GameObject newBestBadge;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button shareButton;

        private bool subscribed;

        public void Configure(GameStateManager state, ScoreManager score, ShareService share)
        {
            Unsubscribe();
            stateManager = state;
            scoreManager = score;
            shareService = share;
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
            if (playAgainButton != null)
            {
                playAgainButton.onClick.AddListener(OnPlayAgain);
            }
            if (shareButton != null)
            {
                shareButton.onClick.AddListener(OnShare);
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
            if (playAgainButton != null)
            {
                playAgainButton.onClick.RemoveListener(OnPlayAgain);
            }
            if (shareButton != null)
            {
                shareButton.onClick.RemoveListener(OnShare);
            }
            subscribed = false;
        }

        private void ApplyState(GameState state)
        {
            bool visible = state == GameState.GameOver;
            if (visible)
            {
                Populate();
            }
            if (panel != null && panel.activeSelf != visible)
            {
                panel.SetActive(visible);
            }
        }

        private void Populate()
        {
            if (scoreManager == null)
            {
                return;
            }

            // Commit before reading so BestScore/IsNewBest reflect the run that just ended.
            scoreManager.CommitBestScore();

            if (finalScoreLabel != null)
            {
                finalScoreLabel.SetText(scoreManager.Score.ToString());
            }
            if (bestScoreLabel != null)
            {
                bestScoreLabel.SetText($"Best {scoreManager.BestScore}");
            }
            if (newBestBadge != null)
            {
                bool isNewBest = scoreManager.IsNewBest;
                if (newBestBadge.activeSelf != isNewBest)
                {
                    newBestBadge.SetActive(isNewBest);
                }
            }
        }

        private void OnPlayAgain()
        {
            if (stateManager != null)
            {
                stateManager.RestartToAttract();
            }
        }

        private void OnShare()
        {
            if (shareService == null || scoreManager == null)
            {
                return;
            }
            shareService.ShareScoreCard(scoreManager.Score, scoreManager.BestScore, scoreCardRoot, uiCamera);
        }
    }
}
