using UnityEngine;

namespace FlappyVoice.Platform
{
    /// <summary>
    /// How a share ended, so the end screen can say something true about it. The browser's share
    /// is the only path that can be refused or dismissed, and a dismissal is a choice rather than
    /// a fault, so it is kept apart from a real failure.
    /// </summary>
    public enum ShareOutcome
    {
        Shared,
        Copied,
        Cancelled,
        Failed,
    }

    /// <summary>
    /// A way out of the game for a score. Exists so `ShareService` can render the card and compose
    /// the message once and stay ignorant of whether the platform under it has a share sheet, a
    /// clipboard, or nothing but a log.
    /// </summary>
    public interface INativeShare
    {
        /// <summary>
        /// Whether this backend does anything with a captured score card. Rendering one costs a
        /// full-resolution readback and a PNG encode on the main thread, so a backend that cannot
        /// attach a file says so rather than being made to pay for one it will drop.
        /// </summary>
        bool WantsScoreCard { get; }

        /// <summary>
        /// The result this backend settles on by itself, or null when it will report one later
        /// through <see cref="ShareService.OnWebShareResult"/>. Only the browser's share is
        /// asynchronous; everything else is done by the time <see cref="Share"/> returns.
        /// </summary>
        ShareOutcome? ImmediateOutcome { get; }

        /// <summary>Hand the score off. <paramref name="filePath"/> may be null.</summary>
        void Share(string filePath, string message, string url);
    }

    /// <summary>
    /// The backend off the web, where there is no share sheet to raise and no clipboard worth
    /// writing to. Prints what would have been shared so the button stays testable in the Editor.
    /// </summary>
    public sealed class LogOnlyNativeShare : INativeShare
    {
        /// <summary>True, so the Editor still exercises the capture and a broken card is visible.</summary>
        public bool WantsScoreCard => true;

        /// <summary>Done by the time the log line is written; nothing arrives later.</summary>
        public ShareOutcome? ImmediateOutcome => ShareOutcome.Shared;

        /// <summary>Log the message, the link and the captured card's path.</summary>
        public void Share(string filePath, string message, string url)
        {
            Debug.Log($"[Share] {message} {url} :: {filePath}");
        }
    }
}
