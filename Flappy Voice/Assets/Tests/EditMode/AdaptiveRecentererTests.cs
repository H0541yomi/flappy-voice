using System;
using FlappyVoice.Gameplay;
using NUnit.Framework;

namespace FlappyVoice.Tests
{
    // AdaptiveRecenterer is no longer wired into VoiceHeightSource: the A-to-A mechanic requires the
    // octave floor to stay exactly on an A, and drifting it would break every pipe's note letter.
    // The class and these tests are retained unchanged so the behaviour can be revived if the pipe
    // labelling ever stops depending on the floor. They exercise the retained wrap helper, not the
    // live clamped mapping.
    public class AdaptiveRecentererTests
    {
        private const float Dt = 1f / 60f;
        private const float WindowSec = 4f;
        private const float DriftRate = 0.35f;
        private const float EdgeThreshold = 0.15f;
        private const int Width = 12;

        private static AdaptiveRecenterer MakeRecenterer()
        {
            var recenterer = new AdaptiveRecenterer();
            recenterer.Configure(WindowSec, DriftRate, EdgeThreshold, Width);
            return recenterer;
        }

        private static float SeamDistance(float height)
        {
            return height < 1f - height ? height : 1f - height;
        }

        [Test]
        public void MeanNearCentre_LeavesFloorUnchanged()
        {
            var recenterer = MakeRecenterer();
            const float floor = 48f;

            for (int i = 0; i < 600; i++)
            {
                Assert.That(recenterer.Update(0.5f, floor, Dt), Is.EqualTo(floor));
            }

            Assert.That(recenterer.CircularMeanHeight, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(recenterer.IsHuggingEdge, Is.False);
        }

        [Test]
        public void CircularMeanOfSeamStraddlingHeights_IsAtSeamNotCentre()
        {
            var recenterer = MakeRecenterer();
            for (int i = 0; i < 200; i++)
            {
                recenterer.Update(i % 2 == 0 ? 0.98f : 0.02f, 48f, Dt);
            }

            // An arithmetic mean would report 0.5 here; the circular mean must report the seam.
            Assert.That(SeamDistance(recenterer.CircularMeanHeight), Is.LessThan(0.02f));
            Assert.That(Math.Abs(recenterer.CircularMeanHeight - 0.5f), Is.GreaterThan(0.4f));
            Assert.That(recenterer.IsHuggingEdge, Is.True);
        }

        // AdaptiveRecenterer is still built around a wrapping octave and is still not wired into
        // gameplay. PitchMath no longer ships a wrap - the game clamps - so the wrap these tests
        // exercise lives here, with the only component that still assumes it.
        private static float WrapHeight(float midi, float floor, int width)
        {
            if (width <= 0) return 0f;
            float rel = (midi - floor) % width;
            if (rel < 0f) rel += width;
            float height = rel / width;
            return height >= 0f && height < 1f ? height : 0f;
        }

        [TestCase(0.98f)]
        [TestCase(0.02f)]
        public void HuggingSeam_DriftsFloorAndPushesMeanTowardCentre(float startHeight)
        {
            var recenterer = MakeRecenterer();
            float floor = 48f;
            float midi = floor + startHeight * Width;

            float initialDistanceFromCentre = Math.Abs(startHeight - 0.5f);
            float maxPerFrameChange = 0f;

            for (int i = 0; i < (int)(30f / Dt); i++)
            {
                float height = WrapHeight(midi, floor, Width);
                float next = recenterer.Update(height, floor, Dt);

                float change = Math.Abs(next - floor);
                if (change > maxPerFrameChange) maxPerFrameChange = change;
                floor = next;
            }

            Assert.That(floor, Is.Not.EqualTo(48f), "floor should have drifted");
            Assert.That(maxPerFrameChange, Is.LessThanOrEqualTo(DriftRate * Dt + 1e-5f));

            float finalHeight = WrapHeight(midi, floor, Width);
            Assert.That(SeamDistance(finalHeight), Is.GreaterThan(EdgeThreshold));
            Assert.That(Math.Abs(recenterer.CircularMeanHeight - 0.5f), Is.LessThan(initialDistanceFromCentre));
            Assert.That(recenterer.IsHuggingEdge, Is.False);
        }

        [Test]
        public void HuggingTop_RaisesFloor_HuggingBottom_LowersFloor()
        {
            // height = ((midi - floor) mod width) / width, so raising the floor lowers the height.
            var top = MakeRecenterer();
            float topFloor = 48f;
            for (int i = 0; i < 120; i++) topFloor = top.Update(0.97f, topFloor, Dt);
            Assert.That(topFloor, Is.GreaterThan(48f));

            var bottom = MakeRecenterer();
            float bottomFloor = 48f;
            for (int i = 0; i < 120; i++) bottomFloor = bottom.Update(0.03f, bottomFloor, Dt);
            Assert.That(bottomFloor, Is.LessThan(48f));
        }

        [Test]
        public void PerFrameChangeNeverExceedsDriftRate()
        {
            var recenterer = MakeRecenterer();
            float floor = 48f;
            var random = new Random(99);

            for (int i = 0; i < 2000; i++)
            {
                float dt = 0.008f + (float)random.NextDouble() * 0.03f;
                float height = (float)random.NextDouble() * 0.06f;
                if (i % 3 == 0) height = 1f - height;

                float next = recenterer.Update(height, floor, dt);
                Assert.That(Math.Abs(next - floor), Is.LessThanOrEqualTo(DriftRate * dt + 1e-5f));
                floor = next;
            }
        }

        [Test]
        public void MeanIsAlwaysInUnitInterval()
        {
            var recenterer = MakeRecenterer();
            var random = new Random(7);
            for (int i = 0; i < 1000; i++)
            {
                recenterer.Update((float)random.NextDouble(), 48f, Dt);
                Assert.That(recenterer.CircularMeanHeight, Is.GreaterThanOrEqualTo(0f));
                Assert.That(recenterer.CircularMeanHeight, Is.LessThan(1f));
            }
        }

        [Test]
        public void Reset_ClearsWindow()
        {
            var recenterer = MakeRecenterer();
            for (int i = 0; i < 300; i++) recenterer.Update(0.99f, 48f, Dt);
            Assert.That(recenterer.IsHuggingEdge, Is.True);

            recenterer.Reset();

            Assert.That(recenterer.IsHuggingEdge, Is.False);
            Assert.That(recenterer.CircularMeanHeight, Is.EqualTo(0.5f).Within(1e-6f));

            Assert.That(recenterer.Update(0.5f, 48f, Dt), Is.EqualTo(48f));
            Assert.That(recenterer.CircularMeanHeight, Is.EqualTo(0.5f).Within(1e-3f));
        }
    }
}
