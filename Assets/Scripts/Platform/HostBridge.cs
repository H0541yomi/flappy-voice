using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FlappyVoice.Platform
{
    /// <summary>
    /// Way out of a game that has no way out of its own: the Web build runs inside the Variant
    /// host, which owns the frame around it, so quitting means posting a message to the host
    /// rather than closing anything. Thin wrapper over Assets/Plugins/WebGL/FlappyVoiceHost.jslib.
    /// </summary>
    public static class HostBridge
    {
        /// <summary>
        /// Whether a host is reachable at all. False off the web, where the jslib does not exist
        /// and there is nothing embedding the game.
        /// </summary>
#if UNITY_WEBGL && !UNITY_EDITOR
        public const bool IsBackend = true;
#else
        public const bool IsBackend = false;
#endif

        // The payload FlappyVoiceHost.jslib posts, kept here only so the log above can show it.
        // The jslib builds its own copy: it is the one that has a window to post from, and a C#
        // string marshalled across for every quit would be a second place to get the schema
        // wrong. Keep the two in step.
        private const string QuitMessage = "{\"schema_version\":1,\"action\":\"quit\"}";

        /// <summary>
        /// Asks the host to close the game. Fire-and-forget: the host decides what happens next
        /// and there is no reply to wait for, so callers must not expect this to have torn
        /// anything down by the time it returns. A no-op wherever there is no host.
        /// </summary>
        public static void RequestQuit()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                Debug.Log($"[HostBridge] posting to VariantOriginalsHost: {QuitMessage}");
                FV_Host_Quit();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HostBridge] quit call failed: {e.Message}");
            }
#else
            // Off the web there is no host to ask, and Application.Quit is a lie in the editor.
            // Logging the message the jslib would post keeps the button testable in the Editor -
            // a tap that prints this is a tap that reaches window.VariantOriginalsHost on Web.
            Debug.Log($"[HostBridge] quit requested; on Web this posts to VariantOriginalsHost: {QuitMessage}");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void FV_Host_Quit();
#endif
    }
}
