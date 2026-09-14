using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using FlappyVoice.UI;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    // The tuner strip is opaque and sits at the top of the screen, so the playfield has to end
    // below it: a bird at the top of its range must still be visible, and so must the highest gap
    // a pipe can be given. Nothing in the running game states that - it falls out of the camera
    // size, the canvas match mode and the strip's authored height together - so it is pinned here.
    public class HudLayoutTests
    {
        private GameConfig config;

        [SetUp]
        public void SetUp()
        {
            config = GameConfig.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (config != null)
            {
                Object.DestroyImmediate(config);
            }
        }

        [TestCase(0f)]
        [TestCase(0.05f)]
        [TestCase(0.1042f)]
        [TestCase(0.2f)]
        [TestCase(0.4f)]
        public void PlayfieldAndItsClearanceFitUnderTheStrip(float hudFraction)
        {
            const float bottomMargin = 1f;
            float topClearance = TopClearance();

            HudLayout.CameraForPlayfield(config.PlayfieldMinY, config.PlayfieldMaxY, bottomMargin,
                topClearance, hudFraction, out float size, out float centerY);

            float hudBottom = HudLayout.HudBottomY(size, centerY, hudFraction);

            const float epsilon = 1e-3f;
            Assert.That(hudBottom, Is.GreaterThanOrEqualTo(config.PlayfieldMaxY + topClearance - epsilon),
                $"the strip overlaps the top of the playfield at fraction {hudFraction}");
            Assert.That(centerY - size, Is.LessThanOrEqualTo(config.PlayfieldMinY - bottomMargin + epsilon),
                "the bottom of the playfield is off the bottom of the screen");
        }

        // The two things that must stay visible under the strip: the bird held at the top of its
        // range, and the opening of the highest gap a pipe can be given.
        [Test]
        public void BirdAndHighestGapBothClearTheStrip()
        {
            const float hudFraction = 0.1042f;
            float topClearance = TopClearance();

            HudLayout.CameraForPlayfield(config.PlayfieldMinY, config.PlayfieldMaxY, 1f, topClearance,
                hudFraction, out float size, out float centerY);
            float hudBottom = HudLayout.HudBottomY(size, centerY, hudFraction);

            float birdTop = config.PlayfieldMaxY + config.PlayerBodyRadiusUnits;
            float gapTop = config.PlayfieldMaxY + (config.PipeGapSizeAtDifficulty(0f) * 0.5f);

            Assert.That(birdTop, Is.LessThan(hudBottom), "the bird can fly behind the tuner");
            Assert.That(gapTop, Is.LessThan(hudBottom), "a gap can open behind the tuner");
        }

        // A strip tall enough to be absurd must still produce a usable camera rather than a
        // negative or exploding one.
        [Test]
        public void AnAbsurdlyTallStripStillProducesAUsableCamera()
        {
            HudLayout.CameraForPlayfield(config.PlayfieldMinY, config.PlayfieldMaxY, 1f, 1f, 0.95f,
                out float size, out float centerY);

            Assert.That(size, Is.GreaterThan(1f));
            Assert.That(float.IsNaN(centerY), Is.False);
        }

        // The highest gap, not the bird, is what sets the clearance: the gap opening is taller
        // than the bird is, at every point on the ramp.
        private float TopClearance()
        {
            float gapHalf = config.PipeGapSizeAtDifficulty(0f) * 0.5f;
            return Mathf.Max(config.PlayerBodyRadiusUnits, gapHalf) + 0.25f;
        }
    }
}
