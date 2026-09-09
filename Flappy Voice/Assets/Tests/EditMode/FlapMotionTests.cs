using FlappyVoice.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    public class FlapMotionTests
    {
        private const float Period = 1f / 3.4f;
        private const float PeakToPeak = 0.45f;
        private const float Rise = 0.42f;

        private static float Offset(float t)
        {
            return PlayerController.FlapOffset(t, Period, PeakToPeak, Rise);
        }

        [Test]
        public void TotalTravelEqualsConfiguredPeakToPeak()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i <= 2000; i++)
            {
                float value = Offset(Period * i / 2000f);
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }

            // The gap budget is spent against this number, so it must not exceed what was configured.
            Assert.That(max - min, Is.EqualTo(PeakToPeak).Within(1e-3f));
        }

        [Test]
        public void ApexIsAtTheEndOfTheRisePhase()
        {
            float apexTime = Period * Rise;
            float apex = Offset(apexTime);

            for (int i = 0; i <= 2000; i++)
            {
                Assert.That(Offset(Period * i / 2000f), Is.LessThanOrEqualTo(apex + 1e-4f));
            }
        }

        [Test]
        public void BottomOfTheStrokeIsAtTheCycleBoundary()
        {
            float bottom = Offset(0f);

            for (int i = 0; i <= 2000; i++)
            {
                Assert.That(Offset(Period * i / 2000f), Is.GreaterThanOrEqualTo(bottom - 1e-4f));
            }
        }

        // The cusp is the whole point: a sine eases through the bottom and reads as floating, while
        // an abrupt reversal reads as a bounce.
        [Test]
        public void BottomIsACusp_NotASmoothTurn()
        {
            const float h = 1e-4f;
            float slopeBefore = (Offset(Period - h) - Offset(Period - (2f * h))) / h;
            float slopeAfter = (Offset(h) - Offset(0f)) / h;

            Assert.That(slopeBefore, Is.LessThan(-1f), "must still be falling hard at the bottom");
            Assert.That(slopeAfter, Is.GreaterThan(1f), "must launch hard out of the bottom");
        }

        [Test]
        public void RiseIsFasterThanFall_WhenRiseFractionIsBelowHalf()
        {
            float riseSeconds = Period * Rise;
            float fallSeconds = Period - riseSeconds;
            Assert.That(riseSeconds, Is.LessThan(fallSeconds));
        }

        [Test]
        public void IsPeriodic()
        {
            for (int i = 0; i <= 50; i++)
            {
                float t = Period * i / 50f;
                Assert.That(Offset(t + Period), Is.EqualTo(Offset(t)).Within(1e-4f));
                Assert.That(Offset(t + (5f * Period)), Is.EqualTo(Offset(t)).Within(1e-3f));
            }
        }

        [Test]
        public void TimeAverageIsNearZero_SoTheBirdReadsAtItsSungHeight()
        {
            double sum = 0.0;
            const int samples = 20000;
            for (int i = 0; i < samples; i++)
            {
                sum += Offset(Period * (i + 0.5f) / samples);
            }

            Assert.That(sum / samples, Is.EqualTo(0.0).Within(1e-3));
        }

        [Test]
        public void DegenerateInputs_ReturnZero()
        {
            Assert.That(PlayerController.FlapOffset(0.1f, 0f, PeakToPeak, Rise), Is.EqualTo(0f));
            Assert.That(PlayerController.FlapOffset(0.1f, -1f, PeakToPeak, Rise), Is.EqualTo(0f));
            Assert.That(PlayerController.FlapOffset(0.1f, Period, 0f, Rise), Is.EqualTo(0f));
        }

        [TestCase(0f)]
        [TestCase(0.01f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(-1f)]
        public void ExtremeRiseFractions_StayWithinTravelBudget(float riseFraction)
        {
            for (int i = 0; i <= 500; i++)
            {
                float value = PlayerController.FlapOffset(Period * i / 500f, Period, PeakToPeak, riseFraction);
                Assert.That(Mathf.Abs(value), Is.LessThanOrEqualTo(PeakToPeak + 1e-4f));
            }
        }
    }
}
