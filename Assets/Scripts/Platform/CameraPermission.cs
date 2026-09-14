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
            if (HasPermission)
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

            onResult?.Invoke(HasPermission);
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
                    onResult?.Invoke(PickFrontFacing(devices));
                    yield break;
                }

                yield return null;
            }

            onResult?.Invoke(null);
        }

        // Opening WebCamTexture with no name takes the browser's default, which on most phones is
        // the rear camera — a selfie background pointed at the floor.
        private static string PickFrontFacing(WebCamDevice[] devices)
        {
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].isFrontFacing)
                {
                    return devices[i].name;
                }
            }

            // Desktop browsers report isFrontFacing false for a laptop's built-in camera, so a
            // list with no front-facing entry still means "use what there is", not "give up".
            return devices[0].name;
        }
    }
}
