using System;
using System.Collections;
using UnityEngine;

namespace FlappyVoice.Platform
{
    public static class MicPermission
    {
        public static bool HasPermission
        {
            get
            {
                try
                {
                    return Application.HasUserAuthorization(UserAuthorization.Microphone);
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

            onResult?.Invoke(HasPermission);
        }
    }
}
