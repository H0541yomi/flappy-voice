using System.Collections;
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
        // The best line is a row of two labels, not one string: the numeral is set in the number
        // face and the word beside it in the sign face, so they cannot share a TMP_Text.
        [SerializeField] private GameObject bestScoreRow;
        [SerializeField] private TextMeshProUGUI bestScoreLabel;
        [SerializeField] private GameObject newBestBadge;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button shareButton;

        // Long enough to read at a glance, short enough that a second tap is not blocked by it.
        private const float ShareNoticeSeconds = 2f;

        private bool subscribed;
        private Coroutine shareLabelRestore;
        // Captured once, not per notice: a second share while the first notice is still up would
        // otherwise restore the button to "Link copied" and leave it there for good.
        private string shareLabelAuthored;

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
            if (shareService != null)
            {
                shareService.OnShareResult += ApplyShareResult;
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
            if (shareService != null)
            {
                shareService.OnShareResult -= ApplyShareResult;
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
            bool isNewBest = scoreManager.IsNewBest;

            if (bestScoreLabel != null)
            {
                bestScoreLabel.SetText(scoreManager.BestScore.ToString());
            }
            // The badge sits on this line rather than in a row of its own, and on a new best
            // "BEST 42" only repeats the 42 already above it. Toggling the ROW, because the word
            // and the numeral are two objects now and hiding one would leave the other stranded.
            if (bestScoreRow != null && bestScoreRow.activeSelf == isNewBest)
            {
                bestScoreRow.SetActive(!isNewBest);
            }
            if (newBestBadge != null && newBestBadge.activeSelf != isNewBest)
            {
                newBestBadge.SetActive(isNewBest);
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

        /// <summary>
        /// Say what the tap did. A browser with no share sheet puts the link on the clipboard, and
        /// a clipboard write looks exactly like nothing happening unless the button says so.
        /// </summary>
        private void ApplyShareResult(ShareOutcome outcome)
        {
            if (shareButton == null)
            {
                return;
            }
            // Dismissing the share sheet already told the player what happened; saying it again on
            // the button would read as an error.
            if (outcome == ShareOutcome.Cancelled)
            {
                return;
            }

            string notice = outcome switch
            {
                ShareOutcome.Copied => "Link copied",
                ShareOutcome.Failed => "Share failed",
                _ => null,
            };
            if (notice == null)
            {
                return;
            }

            if (shareLabelRestore != null)
            {
                StopCoroutine(shareLabelRestore);
            }
            shareLabelRestore = StartCoroutine(ShowShareNoticeRoutine(notice));
        }

        /// <summary>
        /// Swap the share button's own label for the notice and put it back. The label is found
        /// rather than serialized because it is that button's only child text -- a second
        /// reference to it would be one more thing to keep in step with `shareButton`.
        /// </summary>
        private IEnumerator ShowShareNoticeRoutine(string notice)
        {
            TextMeshProUGUI label = shareButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label == null)
            {
                shareLabelRestore = null;
                yield break;
            }

            shareLabelAuthored ??= label.text;
            label.text = notice;
            yield return new WaitForSecondsRealtime(ShareNoticeSeconds);
            // The end screen may be long gone by now; the label object outlives it either way, and
            // leaving a notice on the button is what the next game over would inherit.
            if (label != null)
            {
                label.text = shareLabelAuthored;
            }
            shareLabelRestore = null;
        }
    }
}
