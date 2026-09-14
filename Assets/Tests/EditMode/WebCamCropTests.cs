using FlappyVoice.Video;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests.EditMode
{
    public class WebCamCropTests
    {
        private const float Tolerance = 1e-4f;

        // 1080x1920 portrait against a 640x480 webcam - the shipping case.
        private const float PortraitView = 1080f / 1920f;
        private const float LandscapeView = 1920f / 1080f;
        private const float WebcamSource = 640f / 480f;

        private static WebCamCropRect Cover(float viewAspect, float sourceAspect, float zoom = 1f)
        {
            return WebCamCrop.Cover(viewAspect, sourceAspect, zoom, Vector2.zero, false, false);
        }

        // The whole point of the crop: the sampled window has to be the shape of the view, or the
        // stretch onto the quad is uneven and faces come out squashed.
        [TestCase(PortraitView, WebcamSource)]
        [TestCase(LandscapeView, WebcamSource)]
        [TestCase(PortraitView, 16f / 9f)]
        [TestCase(LandscapeView, 3f / 4f)]
        [TestCase(1f, 1f)]
        public void WindowMatchesViewAspect(float viewAspect, float sourceAspect)
        {
            WebCamCropRect crop = Cover(viewAspect, sourceAspect);

            float windowAspect = sourceAspect * (crop.Scale.x / crop.Scale.y);
            Assert.That(windowAspect, Is.EqualTo(viewAspect).Within(Tolerance));
        }

        // A scale above 1 samples past the edge of the texture, where a clamped sampler smears
        // its last row of pixels across the screen.
        [TestCase(PortraitView, WebcamSource)]
        [TestCase(LandscapeView, WebcamSource)]
        [TestCase(PortraitView, 16f / 9f)]
        public void WindowStaysInsideTheTexture(float viewAspect, float sourceAspect)
        {
            WebCamCropRect crop = Cover(viewAspect, sourceAspect);

            Assert.That(crop.Offset.x, Is.GreaterThanOrEqualTo(-Tolerance));
            Assert.That(crop.Offset.y, Is.GreaterThanOrEqualTo(-Tolerance));
            Assert.That(crop.Offset.x + crop.Scale.x, Is.LessThanOrEqualTo(1f + Tolerance));
            Assert.That(crop.Offset.y + crop.Scale.y, Is.LessThanOrEqualTo(1f + Tolerance));
        }

        // Cover, not contain: one axis is always shown whole, so nothing is ever letterboxed.
        [TestCase(PortraitView, WebcamSource)]
        [TestCase(LandscapeView, WebcamSource)]
        public void OneAxisIsAlwaysUncropped(float viewAspect, float sourceAspect)
        {
            WebCamCropRect crop = Cover(viewAspect, sourceAspect);

            Assert.That(Mathf.Max(crop.Scale.x, crop.Scale.y), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void PortraitViewCropsTheSidesAndKeepsFullHeight()
        {
            WebCamCropRect crop = Cover(PortraitView, WebcamSource);

            Assert.That(crop.Scale.x, Is.EqualTo(0.421875f).Within(Tolerance));
            Assert.That(crop.Scale.y, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(crop.Offset.x, Is.EqualTo(0.2890625f).Within(Tolerance));
            Assert.That(crop.Offset.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void LandscapeViewCropsTopAndBottomAndKeepsFullWidth()
        {
            WebCamCropRect crop = Cover(LandscapeView, WebcamSource);

            Assert.That(crop.Scale.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(crop.Scale.y, Is.EqualTo(0.75f).Within(Tolerance));
            Assert.That(crop.Offset.y, Is.EqualTo(0.125f).Within(Tolerance));
        }

        [Test]
        public void ZoomShrinksTheWindowAndKeepsItCentred()
        {
            WebCamCropRect none = Cover(PortraitView, WebcamSource);
            WebCamCropRect zoomed = Cover(PortraitView, WebcamSource, 2f);

            Assert.That(zoomed.Scale.x, Is.EqualTo(none.Scale.x * 0.5f).Within(Tolerance));
            Assert.That(zoomed.Scale.y, Is.EqualTo(none.Scale.y * 0.5f).Within(Tolerance));
            Assert.That(zoomed.Offset.x + zoomed.Scale.x * 0.5f, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(zoomed.Offset.y + zoomed.Scale.y * 0.5f, Is.EqualTo(0.5f).Within(Tolerance));
        }

        // A zoom under 1 would want a window larger than the source.
        [Test]
        public void ZoomBelowOneIsIgnored()
        {
            WebCamCropRect none = Cover(PortraitView, WebcamSource);
            WebCamCropRect shrunk = Cover(PortraitView, WebcamSource, 0.25f);

            Assert.That(shrunk.Scale.x, Is.EqualTo(none.Scale.x).Within(Tolerance));
            Assert.That(shrunk.Scale.y, Is.EqualTo(none.Scale.y).Within(Tolerance));
        }

        [Test]
        public void PanCannotPushTheWindowOffTheTexture()
        {
            WebCamCropRect crop = WebCamCrop.Cover(PortraitView, WebcamSource, 2f,
                new Vector2(5f, -5f), false, false);

            Assert.That(crop.Offset.x, Is.EqualTo(1f - crop.Scale.x).Within(Tolerance));
            Assert.That(crop.Offset.y, Is.EqualTo(0f).Within(Tolerance));
        }

        // The cropped axis has slack to slide along even at zoom 1 - that is what was cropped -
        // while the uncropped one is already showing everything and cannot move at all. Framing a
        // face off-centre horizontally therefore needs no zoom; framing it higher does.
        [Test]
        public void PanSlidesTheCroppedAxisAndIsPinnedOnTheOther()
        {
            WebCamCropRect centred = Cover(PortraitView, WebcamSource);
            WebCamCropRect panned = WebCamCrop.Cover(PortraitView, WebcamSource, 1f,
                new Vector2(0.1f, 0.3f), false, false);

            Assert.That(panned.Offset.x, Is.EqualTo(centred.Offset.x + 0.1f).Within(Tolerance));
            Assert.That(panned.Offset.y, Is.EqualTo(0f).Within(Tolerance));
        }

        // Same window, traversed backwards: uv 0 lands on its right edge and uv 1 on its left.
        [Test]
        public void MirroringReversesTheWindowWithoutMovingIt()
        {
            WebCamCropRect plain = Cover(PortraitView, WebcamSource);
            WebCamCropRect mirrored = WebCamCrop.Cover(PortraitView, WebcamSource, 1f,
                Vector2.zero, true, false);

            float atLeftEdge = 0f * mirrored.Scale.x + mirrored.Offset.x;
            float atRightEdge = 1f * mirrored.Scale.x + mirrored.Offset.x;

            Assert.That(atLeftEdge, Is.EqualTo(plain.Offset.x + plain.Scale.x).Within(Tolerance));
            Assert.That(atRightEdge, Is.EqualTo(plain.Offset.x).Within(Tolerance));
        }

        [Test]
        public void VerticalMirroringReversesTheOtherAxis()
        {
            WebCamCropRect plain = Cover(LandscapeView, WebcamSource);
            WebCamCropRect flipped = WebCamCrop.Cover(LandscapeView, WebcamSource, 1f,
                Vector2.zero, false, true);

            float atBottomEdge = 0f * flipped.Scale.y + flipped.Offset.y;
            float atTopEdge = 1f * flipped.Scale.y + flipped.Offset.y;

            Assert.That(atBottomEdge, Is.EqualTo(plain.Offset.y + plain.Scale.y).Within(Tolerance));
            Assert.That(atTopEdge, Is.EqualTo(plain.Offset.y).Within(Tolerance));
        }

        // WebCamTexture reports nothing usable until its first frame; an untransformed sample is
        // the only honest thing to draw with no dimensions to solve against.
        [TestCase(0f, 1.333f)]
        [TestCase(0.5625f, 0f)]
        [TestCase(float.NaN, 1.333f)]
        [TestCase(0.5625f, float.NaN)]
        public void DegenerateAspectsFallBackToTheWholeTexture(float viewAspect, float sourceAspect)
        {
            WebCamCropRect crop = Cover(viewAspect, sourceAspect);

            Assert.That(crop.Scale, Is.EqualTo(Vector2.one));
            Assert.That(crop.Offset, Is.EqualTo(Vector2.zero));
        }
    }
}
