using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FlappyVoice.Platform
{
    /// <summary>
    /// The share backend for the Web build. Thin wrapper over
    /// Assets/Plugins/WebGL/FlappyVoiceShare.jslib, which raises the OS share sheet where
    /// `navigator.share` exists and copies the link to the clipboard where it does not.
    /// </summary>
    public sealed class WebNativeShare : INativeShare
    {
        private readonly string receiverName;

        /// <summary>
        /// <paramref name="receiverName"/> is the GameObject the jslib sends the outcome back to,
        /// because a browser share settles on a promise and there is no return value to wait on.
        /// </summary>
        public WebNativeShare(string receiverName)
        {
            this.receiverName = receiverName;
        }

        /// <summary>
        /// False. The browser share carries title, text and a URL; a file would have to be handed
        /// over as a `File` the page can see, and the card is written into Unity's own filesystem
        /// where the page cannot reach it. Capturing one would be work thrown away every tap.
        /// </summary>
        public bool WantsScoreCard => false;

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>Null: the sheet can sit open for as long as the player likes.</summary>
        public ShareOutcome? ImmediateOutcome => null;
#else
        /// <summary>
        /// Settled, because the branch below only logs. Without this the end screen would wait on
        /// a report that the Editor has no jslib to send.
        /// </summary>
        public ShareOutcome? ImmediateOutcome => ShareOutcome.Shared;
#endif

        /// <summary>
        /// Ask the browser to share. <paramref name="filePath"/> is ignored, per
        /// <see cref="WantsScoreCard"/>, and is null in practice.
        /// </summary>
        public void Share(string filePath, string message, string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                FV_Share_Link(receiverName, Application.productName, message, url);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebNativeShare] share call failed: {e.Message}");
            }
#else
            Debug.Log($"[WebNativeShare] off the web; would share \"{message}\" with {url}, " +
                      $"reporting back to {receiverName}");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void FV_Share_Link(string receiver, string title, string text, string url);
#endif
    }
}
