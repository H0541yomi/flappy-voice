using System;
using System.Collections;
using UnityEngine;

namespace FlappyVoice.Platform
{
    // The selfie-camera background is decoration, so every refusal here is non-fatal: the caller
    // keeps the painted sky and the run plays exactly as it did before.
    //
    // Unlike the microphone this goes through Unity's own API. WebCamTexture does work on Web — it
    // was AudioClip.GetData, not the capture itself, that forced FlappyVoiceMic.jslib — so there
    // is no reason for a second bridge.
    public static class CameraPermission
    {
        // The AsyncOperation never completes while a prompt sits unanswered, and a prompt the
        // player is ignoring looks exactly like a slow one, so the wait is budgeted.
        private const float RequestTimeoutSec = 30f;
        // WebCamTexture.devices is empty until the grant lands, and stays empty for some frames
        // after it on every platform that has ever been measured.
        private const float DeviceWaitSec = 3f;

#if UNITY_WEBGL && !UNITY_EDITOR
        private const bool IsWeb = true;
#else
        private const bool IsWeb = false;
#endif

        // Same rule as MicPermission: on the web a refusal means "not inside a user gesture yet"
        // rather than "no", so the caller should ask again after a tap.
        public static bool RetriesOnUserGesture => IsWeb;

        // Whether the player has agreed on THIS boot, which is not the same question as
        // HasPermission. The grant outlives the run - iOS and Android remember it across launches
        // and a browser can remember it across visits - so HasPermission may already be true
        // before the player has agreed to anything this time. Consent is per boot, so this is what
        // the early-out below hangs off, and what anything deciding to show the feed should read.
        public static bool GrantedThisSession { get; private set; }

        // Runs before the first scene loads on every boot, so the flag cannot start stale. Also
        // runs on every Editor Play even with Reload Domain turned off, which is the one way a
        // static could otherwise carry a grant over from the previous Play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewSession()
        {
            GrantedThisSession = false;
        }

        public static bool HasPermission
        {
            get
            {
                try
                {
                    return Application.HasUserAuthorization(UserAuthorization.WebCam);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CameraPermission] authorization query failed: {e.Message}");
                    return false;
                }
            }
        }

        public static IEnumerator RequestRoutine(Action<bool> onResult)
        {
            // Both halves matter: the session flag keeps a remembered grant from standing in for
            // the player's agreement this boot, and HasPermission catches a grant revoked in
            // browser or OS settings midway through the run.
            if (GrantedThisSession && HasPermission)
            {
                onResult?.Invoke(true);
                yield break;
            }

            AsyncOperation request = null;
            try
            {
                request = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CameraPermission] request threw: {e.Message}");
            }

            if (request != null)
            {
                float elapsed = 0f;
                while (!request.isDone)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (elapsed >= RequestTimeoutSec)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            // Latched here rather than at the tap: the tap is only a request, and a prompt the
            // player ignored until the timeout is not a grant.
            GrantedThisSession = HasPermission;
            onResult?.Invoke(GrantedThisSession);
        }

        // Hands back the device name to open, or null if none arrived in time. Call only after a
        // grant; before one the list is empty and this just burns its whole budget.
        public static IEnumerator WaitForCameraDevice(Action<string> onResult)
        {
            float deadline = Time.realtimeSinceStartup + DeviceWaitSec;

            while (Time.realtimeSinceStartup < deadline)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices != null && devices.Length > 0)
                {
                    onResult?.Invoke(PickDevice(devices));
                    yield break;
                }

                yield return null;
            }

            onResult?.Invoke(null);
        }

        // Opening WebCamTexture with no name takes the browser's default, which on most phones is
        // the rear camera - a selfie background pointed at the floor. The policy itself is in
        // CameraDeviceChoice; this only translates Unity's struct into it.
        private static string PickDevice(WebCamDevice[] devices)
        {
            CameraDeviceInfo[] candidates = new CameraDeviceInfo[devices.Length];
            for (int index = 0; index < devices.Length; index++)
            {
                candidates[index] = new CameraDeviceInfo(devices[index].name,
                    devices[index].isFrontFacing);
            }

            return CameraDeviceChoice.Pick(candidates);
        }
    }
}
