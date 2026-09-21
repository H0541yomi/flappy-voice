using System.Runtime.InteropServices;
using UnityEngine;

namespace FlappyVoice.Platform
{
    /// <summary>
    /// The few numbers that outlive a page load — the best score and the settings the pause menu
    /// owns. Exists so the Web build writes them where the browser keeps things, rather than into
    /// PlayerPrefs' IndexedDB filesystem that nothing outside this build can read.
    /// </summary>
    // Numbers, not strings, because every value this game persists is one and returning a string
    // from a jslib means allocating into the Unity heap and handing C# a pointer to free.
    //
    // PlayerPrefs is still the backend off the web: there is no localStorage in the Editor, and
    // the Editor is where the best score gets in the way often enough to want it working.
    public static class LocalStore
    {
        public static float GetFloat(string key, float fallback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return (float)FV_Store_GetNumber(key, fallback);
#else
            return PlayerPrefs.GetFloat(key, fallback);
#endif
        }

        public static void SetFloat(string key, float value)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FV_Store_SetNumber(key, value);
#else
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
#endif
        }

        public static int GetInt(string key, int fallback)
        {
            return Mathf.RoundToInt(GetFloat(key, fallback));
        }

        public static void SetInt(string key, int value)
        {
            SetFloat(key, value);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern double FV_Store_GetNumber(string key, double fallback);
        [DllImport("__Internal")] private static extern void FV_Store_SetNumber(string key, double value);
#endif
    }
}
