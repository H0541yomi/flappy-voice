using System;
using UnityEngine;

namespace FlappyVoice.Audio
{
    public sealed class MicrophoneInput : MonoBehaviour
    {
        private const int ClipLengthSec = 1;
        private const int PreferredSampleRate = 44100;

        [SerializeField] private bool startOnEnable;

        private AudioClip _clip;
        private float[] _scratch;
        private string _deviceName;
        private int _sampleRate = PreferredSampleRate;
        private bool _started;

        public bool IsRecording => _started && _clip != null && Microphone.IsRecording(_deviceName);
        public int SampleRate => _sampleRate;
        public string DeviceName => _deviceName;

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
            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                // Also the pre-permission state on Android: the device list stays empty until granted.
                Debug.LogWarning("[MicrophoneInput] No microphone device available (permission may not be granted yet).");
                return false;
            }

            string target = deviceName;
            if (string.IsNullOrEmpty(target))
            {
                target = devices[0];
            }
            else
            {
                bool found = false;
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i] != target) continue;
                    found = true;
                    break;
                }
                if (!found)
                {
                    Debug.LogWarning("[MicrophoneInput] Requested device not found: " + target);
                    return false;
                }
            }

            if (_started)
            {
                if (target == _deviceName && IsRecording) return true;
                StopRecording();
            }

            _sampleRate = ResolveSampleRate(target);
            _clip = Microphone.Start(target, true, ClipLengthSec, _sampleRate);
            if (_clip == null)
            {
                Debug.LogWarning("[MicrophoneInput] Microphone.Start failed for device: " + target);
                return false;
            }

            _deviceName = target;
            _started = true;
            return true;
        }

        public void StopRecording()
        {
            if (!_started) return;
            if (!string.IsNullOrEmpty(_deviceName) && Microphone.IsRecording(_deviceName)) Microphone.End(_deviceName);
            _started = false;
            _clip = null;
            _deviceName = null;
        }

        public int ReadLatest(float[] destination)
        {
            if (destination == null || destination.Length == 0) return 0;
            if (!IsRecording) return 0;

            int clipSamples = _clip.samples;
            if (clipSamples <= 0) return 0;

            int position = Microphone.GetPosition(_deviceName);
            if (position < 0) return 0;
            if (position > clipSamples) position = clipSamples;

            int count = destination.Length < clipSamples ? destination.Length : clipSamples;

            // GetData always fills the whole array it is handed, so every read is exactly `count`
            // samples wide; the newest `count` samples end at `position` and straddle the loop
            // point whenever position < count.
            int start = position - count;

            if (start >= 0)
            {
                if (destination.Length == count)
                {
                    _clip.GetData(destination, start);
                    return count;
                }

                EnsureScratch(count);
                _clip.GetData(_scratch, start);
                Array.Copy(_scratch, 0, destination, 0, count);
                return count;
            }

            EnsureScratch(count);

            int head = position;         // samples already written after the loop point: clip [0, head)
            int tail = count - head;     // older samples still at the very end: clip [clipSamples-tail, clipSamples)

            _clip.GetData(_scratch, 0);
            Array.Copy(_scratch, 0, destination, tail, head);

            _clip.GetData(_scratch, clipSamples - count);
            Array.Copy(_scratch, count - tail, destination, 0, tail);

            return count;
        }

        private void EnsureScratch(int count)
        {
            if (_scratch == null || _scratch.Length != count) _scratch = new float[count];
        }

        private static int ResolveSampleRate(string device)
        {
            int min, max;
            Microphone.GetDeviceCaps(device, out min, out max);
            if (min == 0 && max == 0) return PreferredSampleRate;
            if (PreferredSampleRate < min) return min;
            if (PreferredSampleRate > max) return max;
            return PreferredSampleRate;
        }
    }
}
