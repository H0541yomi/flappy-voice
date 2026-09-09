using System;
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

        private static int PitchClass(float midi)
        {
            int semitone = (int)Math.Round(midi);
            return ((semitone % 12) + 12) % 12;
        }

        private static OctaveAnchor AnchorAtHz(float hz, float clampMinHz = 70f, float clampMaxHz = 700f)
        {
            var anchor = new OctaveAnchor();
            anchor.Configure(0.35f, 1.5f, clampMinHz, clampMaxHz);
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
            // 220 Hz IS A3 (midi 57), so the A floor is the sung note itself.
            Assert.That(anchor.FloorMidi, Is.EqualTo(57f).Within(0.01f));
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
            Assert.That(anchor.FloorMidi, Is.EqualTo(57f).Within(0.01f));
        }

        // ---- A snapping -------------------------------------------------------------------

        [TestCase(45f, 45f)]    // A2 itself
        [TestCase(46f, 45f)]    // A#2
        [TestCase(47f, 45f)]    // B2
        [TestCase(51f, 45f)]    // D#3 -> floor A2, ceiling A3
        [TestCase(55f, 45f)]    // G3
        [TestCase(56.4f, 45f)]  // more than 50 cents under A3 -> still A2
        [TestCase(56.5f, 57f)]  // within 50 cents of A3 -> treated as A3
        [TestCase(57f, 57f)]    // A3 itself
        [TestCase(60f, 57f)]    // C4
        [TestCase(66f, 57f)]    // F#4
        [TestCase(68.4f, 57f)]  // more than 50 cents under A4 -> still A3
        [TestCase(68.5f, 69f)]  // within 50 cents of A4 -> treated as A4
        [TestCase(69f, 69f)]    // A4 itself
        [TestCase(74f, 69f)]    // D5
        public void Anchoring_SnapsFloorToAn_A_WithinFiftyCents(float sungMidi, float expectedFloor)
        {
            var anchor = AnchorAtMidi(sungMidi);

            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(expectedFloor).Within(0.01f));
            Assert.That(PitchClass(anchor.FloorMidi), Is.EqualTo(PitchMath.APitchClass));
        }

        [Test]
        public void Anchoring_FloorIsAlwaysAnA_AcrossSungRange()
        {
            for (float midi = 36f; midi <= 80f; midi += 0.5f)
            {
                var anchor = AnchorAtMidi(midi);
                Assert.That(anchor.IsAnchored, Is.True);
                Assert.That(
                    PitchClass(anchor.FloorMidi),
                    Is.EqualTo(PitchMath.APitchClass),
                    "floor must stay on an A for sung midi " + midi);
            }
        }

        [Test]
        public void Anchoring_FirstSungNoteIsInsideRange_WhenInsideVocalClamp()
        {
            // A note within 50 cents of an A is treated as that A, so the first note can sit up to
            // half a semitone BELOW the floor. Clamping absorbs that: the bird starts on the floor
            // rather than an octave adrift, which is the failure this tolerance exists to prevent.
            for (float midi = 45f; midi <= 77f; midi += 0.5f)
            {
                var anchor = AnchorAtMidi(midi);
                float height = PitchMath.ClampToOctaveHeight(midi, anchor.FloorMidi, 12);
                Assert.That(height, Is.GreaterThanOrEqualTo(0f));
                Assert.That(height, Is.LessThanOrEqualTo(1f));
                Assert.That(midi - anchor.FloorMidi, Is.GreaterThanOrEqualTo(-0.51f));
                Assert.That(midi - anchor.FloorMidi, Is.LessThan(12.01f));
            }
        }

        // ---- vocal-range clamping by WHOLE octaves ----------------------------------------

        [Test]
        public void AbsurdlyLowNote_ShiftsFloorUpByWholeOctaves()
        {
            // 30 Hz ~ midi 22.5 -> A0 = 21, which is below the 70 Hz clamp (midi ~37.17).
            // +12 -> 33 (still low), +12 -> 45 (A2, inside range).
            var anchor = AnchorAtHz(30f);

            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(45f).Within(0.01f));
            Assert.That(PitchClass(anchor.FloorMidi), Is.EqualTo(PitchMath.APitchClass));
        }

        [Test]
        public void AbsurdlyHighNote_ShiftsFloorDownByWholeOctaves()
        {
            // 2000 Hz ~ midi 95.2 -> A5 = 93, above the 700 Hz clamp (midi ~77.04).
            // -12 -> 81 (still high), -12 -> 69 (A4, inside range).
            var anchor = AnchorAtHz(2000f);

            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(69f).Within(0.01f));
            Assert.That(PitchClass(anchor.FloorMidi), Is.EqualTo(PitchMath.APitchClass));
        }

        [Test]
        public void AnchorClampsToConfiguredHzBounds_ByWholeOctaves()
        {
            // 100..400 Hz is midi ~43.35..67.35, so only A2 (45) and A3 (57) fit inside.
            var low = AnchorAtHz(50f, 100f, 400f);
            Assert.That(low.IsAnchored, Is.True);
            Assert.That(low.FloorMidi, Is.EqualTo(45f).Within(0.01f));

            var high = AnchorAtHz(1200f, 100f, 400f);
            Assert.That(high.IsAnchored, Is.True);
            Assert.That(high.FloorMidi, Is.EqualTo(57f).Within(0.01f));
        }

        [Test]
        public void ClampedFloorNeverLandsOffAnA()
        {
            foreach (float hz in new[] { 25f, 40f, 55f, 80f, 130f, 260f, 440f, 900f, 1500f, 3000f })
            {
                var anchor = AnchorAtHz(hz);
                Assert.That(
                    PitchClass(anchor.FloorMidi),
                    Is.EqualTo(PitchMath.APitchClass),
                    "clamping must only shift by whole octaves, hz=" + hz);
            }
        }

        // ---- SetFloorMidi -----------------------------------------------------------------

        [Test]
        public void SetFloorMidi_SnapsToAnAAndAnchors()
        {
            var anchor = MakeAnchor();

            anchor.SetFloorMidi(51f);
            Assert.That(anchor.IsAnchored, Is.True);
            Assert.That(anchor.FloorMidi, Is.EqualTo(45f).Within(0.01f));

            anchor.SetFloorMidi(45f); // already an A -> unchanged
            Assert.That(anchor.FloorMidi, Is.EqualTo(45f).Within(0.01f));

            anchor.SetFloorMidi(56.4f); // more than 50 cents under A3
            Assert.That(anchor.FloorMidi, Is.EqualTo(45f).Within(0.01f));

            anchor.SetFloorMidi(56.9f); // within 50 cents of A3
            Assert.That(anchor.FloorMidi, Is.EqualTo(57f).Within(0.01f));

            anchor.SetFloorMidi(69f);
            Assert.That(anchor.FloorMidi, Is.EqualTo(69f).Within(0.01f));
        }

        [Test]
        public void SetFloorMidi_OutOfRange_ShiftsWholeOctavesIntoRange()
        {
            var anchor = MakeAnchor();

            anchor.SetFloorMidi(0f);   // A below midi 9 is -3; +48 -> 45
            Assert.That(anchor.FloorMidi, Is.EqualTo(45f).Within(0.01f));

            anchor.SetFloorMidi(200f); // A at or below 200 is 189; -120 -> 69
            Assert.That(anchor.FloorMidi, Is.EqualTo(69f).Within(0.01f));
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
            Assert.That(anchor.FloorMidi, Is.EqualTo(57f).Within(0.01f));
        }
    }
}
