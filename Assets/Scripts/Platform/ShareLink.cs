namespace FlappyVoice.Platform
{
    /// <summary>
    /// The public address a shared score points a friend at. Its own file because the slug is the
    /// one piece of the share that comes from outside this repo, and it has to be findable without
    /// reading the share path first.
    /// </summary>
    public static class ShareLink
    {
        /// <summary>
        /// Registered per game by the Variant publishing team: arbitrary slugs do not resolve under
        /// variantapp.us, so this stays a placeholder that is obviously wrong rather than a
        /// plausible guess that would ship as a dead link nobody notices.
        /// </summary>
        public const string GameSlug = "PLACEHOLDER-REPLACE-IN-PROD";

        /// <summary>The URL handed to the browser's share sheet, or copied to the clipboard.</summary>
        public static string Url => $"https://variantapp.us/{GameSlug}";
    }
}
