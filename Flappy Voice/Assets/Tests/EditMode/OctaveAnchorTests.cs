using FlappyVoice.Gameplay;
using NUnit.Framework;

namespace FlappyVoice.Tests
{
    public class OctaveAnchorTests
    {
        private const float Dt = 1f / 60f;

        private static OctaveAnchor MakeAnchor()
        {
            var anchor = new OctaveAnchor();
            anchor.Configure(0.35f, 1.5f, 70f, 700f);
            return anchor;
        }

        [Test]
        public void JitteryPitch_NeverAnchors()
        {
            var anchor = MakeAnchor();
            for (int i = 0; i < 300; i++)
            {
                float hz = (i % 2 == 0) ? 110f : 220f;
                Assert.That(anchor.TryCapture(hz, Dt), Is.False);
            }
            Assert.That(anchor.IsAnchored, Is.False);
        }

        [Test]
        public void StablePitch_AnchorsExactlyOnce()
        {
            var anchor = MakeAnchor();
            int captures = 0;
            for (int i = 0; i < 120; i++)
            {
                if (anchor.TryCapture(220f, Dt)) captures++;
            }

            Assert.That(captures, Is.EqualTo(1));
            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(PitchMath.HzToMidi(220f)).Within(0.01f));
        }

        [Test]
        public void StablePitch_DoesNotAnchorBeforeWindowElapses()
        {
            var anchor = MakeAnchor();
            int frames = 10; // 0.167s, well under the 0.35s window
            for (int i = 0; i < frames; i++)
            {
                Assert.That(anchor.TryCapture(220f, Dt), Is.False);
            }
            Assert.That(anchor.IsAnchored, Is.False);
        }

        [Test]
        public void SmallWobbleWithinTolerance_StillAnchors()
        {
            var anchor = MakeAnchor();
            bool captured = false;
            for (int i = 0; i < 120 && !captured; i++)
            {
                float midi = PitchMath.HzToMidi(220f) + ((i % 2 == 0) ? 0.4f : -0.4f);
                captured = anchor.TryCapture(PitchMath.MidiToHz(midi), Dt);
            }

            Assert.That(captured, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(PitchMath.HzToMidi(220f)).Within(0.5f));
        }

        [Test]
        public void AnchorClampsToConfiguredHzBounds()
        {
            var low = new OctaveAnchor();
            low.Configure(0.2f, 1.5f, 100f, 400f);
            for (int i = 0; i < 120; i++) low.TryCapture(50f, Dt);
            Assert.That(low.IsAnchored, Is.True);
            Assert.That(low.FloorMidi, Is.EqualTo(PitchMath.HzToMidi(100f)).Within(0.01f));

            var high = new OctaveAnchor();
            high.Configure(0.2f, 1.5f, 100f, 400f);
            for (int i = 0; i < 120; i++) high.TryCapture(1200f, Dt);
            Assert.That(high.IsAnchored, Is.True);
            Assert.That(high.FloorMidi, Is.EqualTo(PitchMath.HzToMidi(400f)).Within(0.01f));
        }

        [Test]
        public void SetFloorMidi_ClampsAndAnchors()
        {
            var anchor = MakeAnchor();
            anchor.SetFloorMidi(0f);
            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(PitchMath.HzToMidi(70f)).Within(0.01f));

            anchor.SetFloorMidi(200f);
            Assert.That(anchor.FloorMidi, Is.EqualTo(PitchMath.HzToMidi(700f)).Within(0.01f));
        }

        [Test]
        public void UnvoicedInput_ResetsProgress()
        {
            var anchor = MakeAnchor();
            for (int i = 0; i < 18; i++) anchor.TryCapture(220f, Dt); // 0.30s
            Assert.That(anchor.IsAnchored, Is.False);

            Assert.That(anchor.TryCapture(0f, Dt), Is.False);

            for (int i = 0; i < 18; i++)
            {
                Assert.That(anchor.TryCapture(220f, Dt), Is.False, "progress should have restarted at zero");
            }
            Assert.That(anchor.IsAnchored, Is.False);

            for (int i = 0; i < 10; i++) anchor.TryCapture(220f, Dt);
            Assert.That(anchor.IsAnchored, Is.True);
        }

        [Test]
        public void Reset_ClearsState()
        {
            var anchor = MakeAnchor();
            for (int i = 0; i < 120; i++) anchor.TryCapture(220f, Dt);
            Assert.That(anchor.IsAnchored, Is.True);

            anchor.Reset();

            Assert.That(anchor.IsAnchored, Is.False);
            Assert.That(anchor.FloorMidi, Is.EqualTo(0f));
            Assert.That(anchor.TryCapture(220f, Dt), Is.False);
        }

        [Test]
        public void AlreadyAnchored_TryCaptureReturnsFalse()
        {
            var anchor = MakeAnchor();
            anchor.SetFloorMidi(PitchMath.HzToMidi(220f));
            for (int i = 0; i < 120; i++)
            {
                Assert.That(anchor.TryCapture(330f, Dt), Is.False);
            }
            Assert.That(anchor.FloorMidi, Is.EqualTo(PitchMath.HzToMidi(220f)).Within(0.01f));
        }
    }
}
