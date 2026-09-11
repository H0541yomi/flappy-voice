using UnityEngine;

namespace FlappyVoice.Platform
{
    public interface INativeShare
    {
        void Share(string filePath, string message);
    }

    public sealed class LogOnlyNativeShare : INativeShare
    {
        public void Share(string filePath, string message)
        {
            Debug.Log($"[Share] {message} :: {filePath}");
        }
    }
}
