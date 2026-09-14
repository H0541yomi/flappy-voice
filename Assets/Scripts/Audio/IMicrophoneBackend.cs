namespace FlappyVoice.Audio
{
    // Capture is platform-split because the Web player cannot use UnityEngine.Microphone: the class
    // compiles there from 6000.4 on, but AudioClip.GetData fails while a recording is active, so
    // the only way to read a live signal is the Web Audio bridge in Assets/Plugins/WebGL.
    internal interface IMicrophoneBackend
    {
        bool IsRecording { get; }
        int SampleRate { get; }
        string DeviceName { get; }

        // Zero means "nothing to record from yet" — on Android that is also the pre-permission
        // state, and on the web it is a request the browser has not granted.
        int DeviceCount { get; }

        bool TryStart(string deviceName);
        void Stop();

        // Fills `destination` with the newest samples and returns how many were written.
        int ReadLatest(float[] destination);
    }
}
