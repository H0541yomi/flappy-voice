using FlappyVoice.Platform;
using NUnit.Framework;

namespace FlappyVoice.Tests.EditMode
{
    public sealed class CameraDeviceChoiceTests
    {
        // macOS enumerates the OBS camera extension ahead of the built-in one, and neither
        // reports isFrontFacing on a desktop, so "first device" is the wrong camera.
        [Test]
        public void SkipsVirtualCameraListedFirst()
        {
            CameraDeviceInfo[] devices =
            {
                new CameraDeviceInfo("OBS Virtual Camera", false),
                new CameraDeviceInfo("MacBook Pro Camera", false),
            };

            Assert.AreEqual("MacBook Pro Camera", CameraDeviceChoice.Pick(devices));
        }

        [Test]
        public void PrefersFrontFacingHardwareOverVirtual()
        {
            CameraDeviceInfo[] devices =
            {
                new CameraDeviceInfo("OBS Virtual Camera", true),
                new CameraDeviceInfo("back camera", false),
                new CameraDeviceInfo("front camera", true),
            };

            Assert.AreEqual("front camera", CameraDeviceChoice.Pick(devices));
        }

        // A phone: front-facing is the whole point of a selfie background.
        [Test]
        public void PrefersFrontFacingWhenAllReal()
        {
            CameraDeviceInfo[] devices =
            {
                new CameraDeviceInfo("back camera", false),
                new CameraDeviceInfo("front camera", true),
            };

            Assert.AreEqual("front camera", CameraDeviceChoice.Pick(devices));
        }

        [Test]
        public void FallsBackToVirtualWhenNothingElseExists()
        {
            CameraDeviceInfo[] devices = { new CameraDeviceInfo("OBS Virtual Camera", false) };

            Assert.AreEqual("OBS Virtual Camera", CameraDeviceChoice.Pick(devices));
        }

        [Test]
        public void ReturnsNullWhenNoDevices()
        {
            Assert.IsNull(CameraDeviceChoice.Pick(new CameraDeviceInfo[0]));
            Assert.IsNull(CameraDeviceChoice.Pick(null));
        }

        [TestCase("OBS Virtual Camera", true)]
        [TestCase("Snap Camera", true)]
        [TestCase("DroidCam Source 3", true)]
        [TestCase("MacBook Pro Camera", false)]
        [TestCase("FaceTime HD Camera", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void ClassifiesDeviceNames(string deviceName, bool expectedVirtual)
        {
            Assert.AreEqual(expectedVirtual, CameraDeviceChoice.IsVirtual(deviceName));
        }
    }
}
