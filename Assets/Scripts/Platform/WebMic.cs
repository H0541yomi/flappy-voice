using System;
using System.Runtime.InteropServices;

namespace FlappyVoice.Platform
{
    // Mirrors the status codes in Assets/Plugins/WebGL/FlappyVoiceMic.jslib.
    public enum WebMicStatus
    {
        Idle = 0,
        Pending = 1,
        Running = 2,
        Blocked = 3,
        Unsupported = 4,
    }

    // Thin wrapper over the jslib bridge. Only the Web player has a backing implementation; every
    // other platform reports Unsupported so callers can branch on IsBackend alone.
    public static class WebMic
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public const bool IsBackend = true;
#else
        public const bool IsBackend = false;
#endif

        public static WebMicStatus Status
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return (WebMicStatus)FV_Mic_GetStatus();
#else
                return WebMicStatus.Unsupported;
#endif
            }
        }

        public static int SampleRate
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return FV_Mic_GetSampleRate();
#else
                return 0;
#endif
            }
        }

        public static bool IsSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return FV_Mic_IsSupported() != 0;
#else
                return false;
#endif
            }
        }

        // Fire-and-forget: getUserMedia is async, and a browser that needs a user gesture first
        // leaves the request Blocked until the plugin's own gesture listener retries it. Poll
        // Status rather than expecting this to have done anything by the time it returns.
        public static void Request()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FV_Mic_Request();
#endif
        }

        public static void Stop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FV_Mic_Stop();
#endif
        }

        public static int Read(float[] destination, int count)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (destination == null || count <= 0 || count > destination.Length) return 0;

            // Pinning beats a marshalled copy here: the array is re-read every frame and IL2CPP
            // would otherwise allocate and blit a temporary for each call.
            GCHandle handle = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                return FV_Mic_Read(handle.AddrOfPinnedObject(), count);
            }
            finally
            {
                handle.Free();
            }
#else
            return 0;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int FV_Mic_IsSupported();
        [DllImport("__Internal")] private static extern void FV_Mic_Request();
        [DllImport("__Internal")] private static extern void FV_Mic_Stop();
        [DllImport("__Internal")] private static extern int FV_Mic_GetStatus();
        [DllImport("__Internal")] private static extern int FV_Mic_GetSampleRate();
        [DllImport("__Internal")] private static extern int FV_Mic_Read(IntPtr destination, int count);
#endif
    }
}
