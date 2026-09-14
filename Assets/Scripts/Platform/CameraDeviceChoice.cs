using System.Collections.Generic;

namespace FlappyVoice.Platform
{
    // One camera as the choice cares about it. WebCamDevice has no public constructor, so the
    // policy cannot be tested through a method that takes one.
    public readonly struct CameraDeviceInfo
    {
        public readonly string Name;
        public readonly bool IsFrontFacing;

        public CameraDeviceInfo(string name, bool isFrontFacing)
        {
            Name = name;
            IsFrontFacing = isFrontFacing;
        }
    }

    // Which camera the selfie background opens.
    public static class CameraDeviceChoice
    {
        // Matched as lowercase substrings of the device name. A virtual camera enumerates exactly
        // like hardware and sorts first on macOS, so the plain "take the first one" fallback hands
        // the player whatever OBS is streaming - often this game, which is a mirror tunnel.
        private static readonly string[] VirtualDeviceMarkers =
        {
            "virtual",
            "obs",
            "snap camera",
            "droidcam",
            "epoccam",
            "screen capture",
        };

        // Null when there is nothing to open. Front-facing wins, but only among real hardware:
        // desktop browsers report isFrontFacing false for a laptop's built-in camera, so a
        // front-facing virtual device would otherwise outrank the camera the player is sat at.
        public static string Pick(IReadOnlyList<CameraDeviceInfo> devices)
        {
            if (devices == null || devices.Count == 0)
            {
                return null;
            }

            string firstHardware = null;
            for (int index = 0; index < devices.Count; index++)
            {
                if (IsVirtual(devices[index].Name))
                {
                    continue;
                }

                if (devices[index].IsFrontFacing)
                {
                    return devices[index].Name;
                }

                if (firstHardware == null)
                {
                    firstHardware = devices[index].Name;
                }
            }

            if (firstHardware != null)
            {
                return firstHardware;
            }

            // Every camera here is virtual - a Camo or DroidCam user with no built-in, or a name
            // the markers happen to match. A feed beats the painted sky, so take one anyway.
            for (int index = 0; index < devices.Count; index++)
            {
                if (devices[index].IsFrontFacing)
                {
                    return devices[index].Name;
                }
            }

            return devices[0].Name;
        }

        public static bool IsVirtual(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return false;
            }

            string lowered = deviceName.ToLowerInvariant();
            for (int index = 0; index < VirtualDeviceMarkers.Length; index++)
            {
                if (lowered.Contains(VirtualDeviceMarkers[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
