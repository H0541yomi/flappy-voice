using FlappyVoice.Platform;

namespace FlappyVoice.Audio
{
    // Web Audio capture through Assets/Plugins/WebGL/FlappyVoiceMic.jslib. The JS side owns a one
    // second ring buffer, so a read is a straight copy of the newest samples and there is no loop
    // point to unwrap the way UnityMicrophoneBackend has to.
    //
    // Compiled everywhere so the type is not a Web-only surprise, but every call is a no-op unless
    // WebMic.IsBackend — MicrophoneInput only ever constructs it on the Web player.
    internal sealed class WebMicrophoneBackend : IMicrophoneBackend
    {
        private const string WebDeviceName = "Browser microphone";

        private bool _started;

        public bool IsRecording => _started && WebMic.Status == WebMicStatus.Running;

        // The browser picks the rate, and it is 48000 far more often than the 44100 the native
        // backend asks for, so the detector has to be rebuilt around whatever comes back.
        public int SampleRate => WebMic.SampleRate;

        public string DeviceName => _started ? WebDeviceName : null;

        // The browser never enumerates a device before permission is granted, so "granted" is the
        // only device count this backend can honestly report.
        public int DeviceCount => WebMic.Status == WebMicStatus.Running ? 1 : 0;

        // Device selection is the browser's, not ours: getUserMedia hands back the user's default
        // input and the picker lives in browser UI.
        public bool TryStart(string deviceName)
        {
            if (WebMic.Status != WebMicStatus.Running) return false;
            _started = true;
            return true;
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;
            WebMic.Stop();
        }

        public int ReadLatest(float[] destination)
        {
            if (destination == null || destination.Length == 0) return 0;
            if (!IsRecording) return 0;
            return WebMic.Read(destination, destination.Length);
        }
    }
}
