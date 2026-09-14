using System;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    /// <summary>
    /// The parchment that tells a player the game cannot hear them, shown when a microphone grant
    /// has been asked for and did not arrive. One message and one button, which only dismisses it.
    /// </summary>
    // Not a step of ConsentFlowUI, even though it is the same sign and sits next to the same ask.
    // The consent panel's job is to spend a tap on getUserMedia; this one reports the answer, and
    // it has to be able to come back long after that flow has finished. Folding it in would mean
    // re-entering a completed flow to show a notice.
    //
    // Dismissing changes nothing about the microphone. GameBootstrap's retry loop keeps running
    // underneath, and on the web the OK tap is itself a user gesture the jslib bridge is listening
    // for - so a player who fixed the permission behind the panel gets picked up by the very tap
    // that closes it.
    //
    // Holds HudUI.SetStartScreenSuppressed while it is up, exactly as the consent panel does: two
    // parchments stacked read as one broken one. The two never overlap because GameBootstrap waits
    // for the consent flow to close before showing this.
    public sealed class MicrophoneNoticeUI : MonoBehaviour
    {
        [SerializeField] private HudUI hud;
        [SerializeField] private GameObject root;
        [SerializeField] private Button dismissButton;

        private bool subscribed;

        /// <summary>Raised on the frame the player taps OK, for anything that wants the gesture.</summary>
        public event Action OnDismissed;

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

        /// <summary>Takes the notice down and gives the start sign back.</summary>
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
            if (dismissButton != null)
            {
                dismissButton.onClick.AddListener(OnDismiss);
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }
            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveListener(OnDismiss);
            }
            subscribed = false;
        }

        private void OnDismiss()
        {
            Hide();
            OnDismissed?.Invoke();
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
