using FlappyVoice.Gameplay;
using NUnit.Framework;

namespace FlappyVoice.Tests
{
    public class IdleCycleTests
    {
        [Test]
        public void CyclePlaysOutAndBackWithoutRepeatingEitherEnd()
        {
            // Four frames is six steps, not eight: an end visited twice running is a beat where
            // the bird stops, which is exactly what a plain loop of a one-way morph looks like.
            int[] expected = { 0, 1, 2, 3, 2, 1 };

            for (int step = 0; step < expected.Length * 3; step++)
            {
                Assert.AreEqual(expected[step % expected.Length],
                    PlayerController.PingPongIndex(step, 4),
                    "step " + step);
            }
        }

        [Test]
        public void CycleStaysInRangeForTheShippedFrameCount()
        {
            // Nine frames today - the drop is missing its fifth - so the count is not a round
            // number and the span is odd against it. Every step still has to land on a frame.
            const int frameCount = 9;

            for (int step = 0; step < 1000; step++)
            {
                int index = PlayerController.PingPongIndex(step, frameCount);
                Assert.That(index, Is.InRange(0, frameCount - 1), "step " + step);
            }
        }

        [Test]
        public void EveryFrameIsVisitedOnceACycle()
        {
            const int frameCount = 9;
            int span = (frameCount * 2) - 2;
            bool[] seen = new bool[frameCount];

            for (int step = 0; step < span; step++)
            {
                seen[PlayerController.PingPongIndex(step, frameCount)] = true;
            }

            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void ASingleFrameHasNowhereToGo()
        {
            // Guards the span arithmetic, which goes to zero at one frame and negative at none.
            Assert.AreEqual(0, PlayerController.PingPongIndex(0, 1));
            Assert.AreEqual(0, PlayerController.PingPongIndex(7, 1));
            Assert.AreEqual(0, PlayerController.PingPongIndex(7, 0));
        }
    }
}
