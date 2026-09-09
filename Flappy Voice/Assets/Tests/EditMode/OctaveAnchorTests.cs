using FlappyVoice.Gameplay;
using NUnit.Framework;

namespace FlappyVoice.Tests
{
    public class OctaveAnchorTests
    {
        private const float Dt = 1f / 60f;
        private const int Width = 12;

        private static OctaveAnchor MakeAnchor()
        {
            var anchor = new OctaveAnchor();
            anchor.Configure(0.35f, 1.5f, 70f, 700f, Width);
            return anchor;
        }

        private static OctaveAnchor AnchorAtHz(float hz, float clampMinHz = 70f, float clampMaxHz = 700f)
        {
            var anchor = new OctaveAnchor();
            anchor.Configure(0.35f, 1.5f, clampMinHz, clampMaxHz, Width);
            for (int i = 0; i < 120 && !anchor.IsAnchored; i++)
            {
                anchor.TryCapture(hz, Dt);
            }
            return anchor;
        }

        private static OctaveAnchor AnchorAtMidi(float midi)
        {
            return AnchorAtHz(PitchMath.MidiToHz(midi));
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
            // 220 Hz is A3 (midi 57). Centre 57, so the range runs 51 (D#3) to 63 (D#4).
            Assert.That(anchor.CenterMidi, Is.EqualTo(57f).Within(0.01f));
            Assert.That(anchor.FloorMidi, Is.EqualTo(51f).Within(0.01f));
            Assert.That(anchor.CeilingMidi, Is.EqualTo(63f).Within(0.01f));
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
            Assert.That(anchor.FloorMidi, Is.EqualTo(51f).Within(0.01f));
        }

        // ---- the first note lands at mid-screen ---------------------------------------------

        [TestCase(45f, 39f)]    // A2 sung -> floor D#2
        [TestCase(51f, 45f)]    // D#3 sung -> floor A2
        [TestCase(57f, 51f)]    // A3
        [TestCase(60f, 54f)]    // C4
        [TestCase(69f, 63f)]    // A4
        [TestCase(51.4f, 45f)]  // rounds down to D#3 before centring
        [TestCase(51.6f, 46f)]  // rounds up to E3 before centring
        public void Anchoring_CentresTheRangeOnTheFirstNote(float sungMidi, float expectedFloor)
        {
            var anchor = AnchorAtMidi(sungMidi);

            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(expectedFloor).Within(0.01f));
        }

        // This is the whole point of the centred anchor: whatever the player sings first, the bird
        // starts at mid-screen instead of jumping to wherever a fixed letter happened to fall.
        [Test]
        public void Anchoring_FirstSungNoteMapsToMidScreen()
        {
            for (float midi = 40f; midi <= 76f; midi += 0.25f)
            {
                var anchor = AnchorAtMidi(midi);
                float height = PitchMath.ClampToOctaveHeight(
                    PitchMath.RoundToSemitone(midi), anchor.FloorMidi, Width);

                Assert.That(height, Is.EqualTo(0.5f).Within(1e-4f), "sung midi " + midi);
            }
        }

        [Test]
        public void Anchoring_FloorIsAlwaysAWholeSemitone()
        {
            for (float midi = 40f; midi <= 76f; midi += 0.31f)
            {
                var anchor = AnchorAtMidi(midi);
                Assert.That(
                    anchor.FloorMidi,
                    Is.EqualTo(System.Math.Round((double)anchor.FloorMidi)).Within(1e-4f),
                    "pipe letters need a real note as the floor, sung midi " + midi);
            }
        }

        [Test]
        public void Anchoring_RangeIsExactlyOneWidthWide()
        {
            var anchor = AnchorAtMidi(62f);
            Assert.That(anchor.CeilingMidi - anchor.FloorMidi, Is.EqualTo((float)Width).Within(1e-4f));
        }

        // ---- vocal-range clamping ------------------------------------------------------------

        [Test]
        public void AbsurdlyLowNote_ClampsCentreUpToTheVocalFloor()
        {
            // 30 Hz is midi ~22.5, below the 70 Hz clamp (midi ~37.17), so the centre clamps to
            // that bound and rounds to 37; the floor sits six semitones under it.
            var anchor = AnchorAtHz(30f);

            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.CenterMidi, Is.EqualTo(37f).Within(0.01f));
            Assert.That(anchor.FloorMidi, Is.EqualTo(31f).Within(0.01f));
        }

        [Test]
        public void AbsurdlyHighNote_ClampsCentreDownToTheVocalCeiling()
        {
            // 2000 Hz is midi ~95.2, above the 700 Hz clamp (midi ~77.04).
            var anchor = AnchorAtHz(2000f);

            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.CenterMidi, Is.EqualTo(77f).Within(0.01f));
            Assert.That(anchor.FloorMidi, Is.EqualTo(71f).Within(0.01f));
        }

        [Test]
        public void AnchorClampsToConfiguredHzBounds()
        {
            // 100..400 Hz is midi ~43.35..67.35.
            var low = AnchorAtHz(50f, 100f, 400f);
            Assert.That(low.CenterMidi, Is.EqualTo(43f).Within(0.01f));
            Assert.That(low.FloorMidi, Is.EqualTo(37f).Within(0.01f));

            var high = AnchorAtHz(1200f, 100f, 400f);
            Assert.That(high.CenterMidi, Is.EqualTo(67f).Within(0.01f));
            Assert.That(high.FloorMidi, Is.EqualTo(61f).Within(0.01f));
        }

        // ---- SetCenterMidi --------------------------------------------------------------------

        [Test]
        public void SetCenterMidi_RoundsAndAnchors()
        {
            var anchor = MakeAnchor();

            anchor.SetCenterMidi(51f);
            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(45f).Within(0.01f));

            anchor.SetCenterMidi(51.4f);
            Assert.That(anchor.FloorMidi, Is.EqualTo(45f).Within(0.01f));

            anchor.SetCenterMidi(51.6f);
            Assert.That(anchor.FloorMidi, Is.EqualTo(46f).Within(0.01f));

            anchor.SetCenterMidi(69f);
            Assert.That(anchor.FloorMidi, Is.EqualTo(63f).Within(0.01f));
        }

        [Test]
        public void SetCenterMidi_OutOfRange_ClampsIntoTheVocalRange()
        {
            var anchor = MakeAnchor();

            anchor.SetCenterMidi(0f);
            Assert.That(anchor.CenterMidi, Is.EqualTo(37f).Within(0.01f));

            anchor.SetCenterMidi(200f);
            Assert.That(anchor.CenterMidi, Is.EqualTo(77f).Within(0.01f));
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
            Assert.That(anchor.CenterMidi, Is.EqualTo(0f));
            Assert.That(anchor.TryCapture(220f, Dt), Is.False);
        }

        [Test]
        public void AlreadyAnchored_TryCaptureReturnsFalse()
        {
            var anchor = MakeAnchor();
            anchor.SetCenterMidi(PitchMath.HzToMidi(220f));
            for (int i = 0; i < 120; i++)
            {
                Assert.That(anchor.TryCapture(330f, Dt), Is.False);
            }
            Assert.That(anchor.FloorMidi, Is.EqualTo(51f).Within(0.01f));
        }
    }
}
