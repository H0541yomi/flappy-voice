using UnityEngine;

namespace FlappyVoice.Audio
{
    // Facade over the platform capture backends (see IMicrophoneBackend). Everything above this
    // class — PitchTracker, GameBootstrap — is written against ReadLatest and knows nothing about
    // which one is running.
    public sealed class MicrophoneInput : MonoBehaviour
    {
        [SerializeField] private bool startOnEnable;

        private IMicrophoneBackend _backend;

        public bool IsRecording => Backend.IsRecording;
        public int SampleRate => Backend.SampleRate;
        public string DeviceName => Backend.DeviceName;
        public int DeviceCount => Backend.DeviceCount;

        private IMicrophoneBackend Backend
        {
            get
            {
                if (_backend == null)
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    _backend = new WebMicrophoneBackend();
#else
                    _backend = new UnityMicrophoneBackend();
#endif
                }
                return _backend;
            }
        }

        private void OnEnable()
        {
            if (startOnEnable) TryStartRecording();
        }

        private void OnDisable()
        {
            StopRecording();
        }

        public bool TryStartRecording(string deviceName = null)
        {
            return Backend.TryStart(deviceName);
        }

        public void StopRecording()
        {
            Backend.Stop();
        }

        public int ReadLatest(float[] destination)
        {
            return Backend.ReadLatest(destination);
        }
    }
}
