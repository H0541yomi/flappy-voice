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

        public static bool HasPermission
        {
            get
            {
                try
                {
#if UNITY_ANDROID && !UNITY_EDITOR
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

#if UNITY_ANDROID && !UNITY_EDITOR
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
