using System;
using FlappyVoice.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    /// <summary>
    /// The parchment that tells a player the game cannot hear them, shown when a microphone grant
    /// has been asked for and did not arrive. One message and two answers: ask again, or leave the
    /// game entirely.
    /// </summary>
    // Not a step of ConsentFlowUI, even though it is the same sign and sits next to the same ask.
    // The consent panel's job is to spend a tap on getUserMedia; this one reports the answer, and
    // it has to be able to come back long after that flow has finished. Folding it in would mean
    // re-entering a completed flow to show a notice.
    //
    // "EXIT" is on it because this is the one panel a player can be stuck behind: the message
    // asks for something the game cannot grant itself, and a browser that has denied the
    // permission for the site will keep denying it however many times OK is tapped. It quits to
    // the host exactly as QuitButtonUI does - the corner X is up at the same time, and this is
    // the same way out, said in words next to the reason for wanting it.
    //
    // "OK" does not close the panel. It only asks again: the notice is the one piece of feedback
    // that the game still cannot hear the player, so it stays up until that stops being true.
    // GameBootstrap takes it down from the branch where recording actually starts, and nowhere
    // else - a panel that closed on its own button would tell a player who was refused a second
    // time that something had been fixed.
    //
    // Nothing else about the microphone changes on the tap. GameBootstrap's retry loop keeps
    // running underneath, and on the web the tap is itself a user gesture the jslib bridge is
    // listening for - so a player who fixed the permission behind the panel is picked up by the
    // very tap that asks again.
    //
    // Holds HudUI.SetStartScreenSuppressed while it is up, exactly as the consent panel does: two
    // parchments stacked read as one broken one. The two never overlap because GameBootstrap waits
    // for the consent flow to reach Done before showing this.
    public sealed class MicrophoneNoticeUI : MonoBehaviour
    {
        [SerializeField] private HudUI hud;
        [SerializeField] private GameObject root;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button retryButton;

        private bool subscribed;

        /// <summary>Raised on the frame the player taps OK, to ask for the microphone again.</summary>
        public event Action OnRetryRequested;

        /// <summary>Whether the notice is currently on screen.</summary>
        public bool IsShowing => root != null && root.activeSelf;

        /// <summary>
        /// Replays the edit-time wiring at runtime: the HUD reference lives in a non-serialized
        /// field here, so the scene save discards it and GameBootstrap hands it back.
        /// </summary>
        public void Configure(HudUI hudUi)
        {
            hud = hudUi;
        }

        /// <summary>Puts the notice up and takes the start sign down behind it.</summary>
        public void Show()
        {
            SetShowing(true);
        }

        /// <summary>
        /// Takes the notice down and gives the start sign back. The grant arriving is the only
        /// thing that may call this - see the note on the class.
        /// </summary>
        public void Hide()
        {
            SetShowing(false);
        }

        private void OnEnable()
        {
            Subscribe();
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
            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExit);
            }
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnRetry);
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExit);
            }
            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetry);
            }
            subscribed = false;
        }

        private void OnRetry()
        {
            OnRetryRequested?.Invoke();
        }

        // Left up rather than hidden: RequestQuit is fire-and-forget and the host decides when the
        // frame goes away, so taking the panel down first would leave the player looking at an
        // attract screen they had just asked to leave.
        private void OnExit()
        {
            HostBridge.RequestQuit();
        }

        // Touches the suppression only on a real transition, never on startup. The flag has two
        // owners now, and the consent panel raises it in its own Awake; a notice that asserted
        // "nothing of mine is showing, so nothing is" would drop the consent panel's hold
        // whenever script order happened to run this one second.
        private void SetShowing(bool showing)
        {
            if (IsShowing == showing)
            {
                return;
            }
            if (root != null)
            {
                root.SetActive(showing);
            }
            if (hud != null)
            {
                hud.SetStartScreenSuppressed(showing);
            }
        }
    }
}
