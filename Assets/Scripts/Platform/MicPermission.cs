using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace FlappyVoice.Platform
{
    public static class MicPermission
    {
        private const float AndroidRequestTimeoutSec = 20f;
        private const float WebRequestTimeoutSec = 30f;

        // Only the browser hands out a second chance: the plugin re-runs getUserMedia on the next
        // tap, so a refusal on the web is "not yet" rather than "no" and the caller should keep
        // waiting. A denied Android or iOS prompt will not come back without a trip to Settings.
        public static bool RetriesOnUserGesture => WebMic.IsBackend;

        public static bool HasPermission
        {
            get
            {
                try
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    // Application.HasUserAuthorization reports on Unity's own Microphone stream,
                    // which this game does not use on the web; the bridge is the source of truth.
                    return WebMic.Status == WebMicStatus.Running;
#elif UNITY_ANDROID && !UNITY_EDITOR
                    // Application.HasUserAuthorization is a no-op on Android and always reports true.
                    return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
                    return Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MicPermission] authorization query failed: {e.Message}");
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

#if UNITY_WEBGL && !UNITY_EDITOR
            if (!WebMic.IsSupported)
            {
                Debug.LogWarning("[MicPermission] This browser exposes no getUserMedia; the game cannot hear anything.");
                onResult?.Invoke(false);
                yield break;
            }

            WebMic.Request();

            // getUserMedia is a promise and the permission prompt is modal to the page, so poll
            // until it settles. It settles immediately into Blocked when the browser wants a user
            // gesture first, which is why the caller keeps asking.
            float elapsed = 0f;
            while (WebMic.Status == WebMicStatus.Pending)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= WebRequestTimeoutSec) break;
                yield return null;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            bool requested = false;
            try
            {
                Permission.RequestUserPermission(Permission.Microphone);
                requested = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicPermission] request threw: {e.Message}");
            }

            if (requested)
            {
                // The simple Android request has no completion callback, so poll. Budget only
                // frames this coroutine actually runs, since the dialog suspends the app and
                // would otherwise eat the whole timeout; a regained focus means it was dismissed.
                float elapsed = 0f;
                bool lostFocus = false;

                while (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    if (!Application.isFocused)
                    {
                        lostFocus = true;
                    }
                    else if (lostFocus)
                    {
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    if (elapsed >= AndroidRequestTimeoutSec)
                    {
                        break;
                    }

                    yield return null;
                }
            }
#else
            AsyncOperation request = null;
            try
            {
                request = Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicPermission] request threw: {e.Message}");
            }

            if (request != null)
            {
                yield return request;
            }
#endif

            onResult?.Invoke(HasPermission);
        }
    }
}
