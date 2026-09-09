using System;
using FlappyVoice.Gameplay;
using NUnit.Framework;

namespace FlappyVoice.Tests
{
    public class PitchMathTests
    {
        private static int PitchClass(float midi)
        {
            int semitone = (int)Math.Round(midi);
            return ((semitone % 12) + 12) % 12;
        }

        [Test]
        public void HzToMidi_A440_Is69()
        {
            Assert.That(PitchMath.HzToMidi(440f), Is.EqualTo(69f).Within(1e-4f));
        }

        [Test]
        public void MidiToHz_69_Is440()
        {
            Assert.That(PitchMath.MidiToHz(69f), Is.EqualTo(440f).Within(1e-3f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(-440f)]
        public void HzToMidi_NonPositive_ReturnsZero(float hz)
        {
            Assert.That(PitchMath.HzToMidi(hz), Is.EqualTo(0f));
        }

        [TestCase(82.41f)]
        [TestCase(110f)]
        [TestCase(261.63f)]
        [TestCase(523.25f)]
        public void HzToMidi_MidiToHz_Roundtrips(float hz)
        {
            float midi = PitchMath.HzToMidi(hz);
            Assert.That(PitchMath.MidiToHz(midi), Is.EqualTo(hz).Within(hz * 1e-4f));
        }

        [Test]
        public void HzToMidi_OctaveIsTwelveSemitones()
        {
            Assert.That(PitchMath.HzToMidi(880f) - PitchMath.HzToMidi(440f), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(PitchMath.HzToMidi(220f) - PitchMath.HzToMidi(440f), Is.EqualTo(-12f).Within(1e-4f));
        }

        // ---- A floor selection -------------------------------------------------------------

        [Test]
        public void APitchClass_MatchesA4()
        {
            Assert.That(PitchMath.APitchClass, Is.EqualTo(9));
            Assert.That(PitchMath.A4Midi % 12, Is.EqualTo(PitchMath.APitchClass));
        }

        [TestCase(9f, 9f)]
        [TestCase(21f, 21f)]
        [TestCase(45f, 45f)]
        [TestCase(57f, 57f)]
        [TestCase(69f, 69f)]
        public void NearestAFloorAtOrBelow_ExactA_IsItself(float midi, float expected)
        {
            Assert.That(PitchMath.NearestAFloorAtOrBelow(midi), Is.EqualTo(expected).Within(1e-4f));
        }

        [TestCase(45.01f, 45f)]
        [TestCase(45.5f, 45f)]
        [TestCase(46f, 45f)]
        [TestCase(56.99f, 45f)]
        [TestCase(44.99f, 33f)]
        [TestCase(44f, 33f)]
        [TestCase(57.5f, 57f)]
        [TestCase(68.5f, 57f)]
        public void NearestAFloorAtOrBelow_JustAboveOrBelowAnA(float midi, float expected)
        {
            Assert.That(PitchMath.NearestAFloorAtOrBelow(midi), Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void NearestAFloorAtOrBelow_DSharp3_IsA2()
        {
            // D#3 = midi 51 -> floor A2 = 45, ceiling A3 = 57.
            Assert.That(PitchMath.NearestAFloorAtOrBelow(51f), Is.EqualTo(45f).Within(1e-4f));
        }

        [TestCase(60f, 57f)]   // C4
        [TestCase(62f, 57f)]   // D4
        [TestCase(48f, 45f)]   // C3
        [TestCase(0f, -3f)]    // below midi 9 the floor goes negative but stays an A
        [TestCase(-1f, -3f)]
        public void NearestAFloorAtOrBelow_KnownNotes(float midi, float expected)
        {
            Assert.That(PitchMath.NearestAFloorAtOrBelow(midi), Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void NearestAFloorAtOrBelow_AlwaysAnAAtOrBelowInput()
        {
            for (float midi = -20f; midi <= 130f; midi += 0.31f)
            {
                float floor = PitchMath.NearestAFloorAtOrBelow(midi);
                Assert.That(PitchClass(floor), Is.EqualTo(PitchMath.APitchClass), "floor must be an A");
                Assert.That(floor, Is.LessThanOrEqualTo(midi + 1e-4f));
                Assert.That(midi - floor, Is.LessThan(12f + 1e-4f));
            }
        }

        // ---- clamped A-to-A height --------------------------------------------------------

        [Test]
        public void ClampToOctaveHeight_AtFloor_IsZero()
        {
            Assert.That(PitchMath.ClampToOctaveHeight(45f, 45f, 12), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ClampToOctaveHeight_HalfOctave_IsHalf()
        {
            Assert.That(PitchMath.ClampToOctaveHeight(51f, 45f, 12), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void ClampToOctaveHeight_AtCeiling_IsOne()
        {
            // The top A is a real, distinct position now - it does NOT wrap back to zero.
            Assert.That(PitchMath.ClampToOctaveHeight(57f, 45f, 12), Is.EqualTo(1f).Within(1e-5f));
        }

        [TestCase(44f)]
        [TestCase(40f)]
        [TestCase(33f)]
        [TestCase(0f)]
        public void ClampToOctaveHeight_BelowFloor_PinsToBottom(float midi)
        {
            Assert.That(PitchMath.ClampToOctaveHeight(midi, 45f, 12), Is.EqualTo(0f).Within(1e-6f));
        }

        [TestCase(58f)]
        [TestCase(64f)]
        [TestCase(69f)]
        [TestCase(120f)]
        public void ClampToOctaveHeight_AboveCeiling_PinsToTop(float midi)
        {
            Assert.That(PitchMath.ClampToOctaveHeight(midi, 45f, 12), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void ClampToOctaveHeight_IsMonotonicAndInUnitInterval()
        {
            float previous = -1f;
            for (float midi = 20f; midi <= 90f; midi += 0.17f)
            {
                float height = PitchMath.ClampToOctaveHeight(midi, 45f, 12);
                Assert.That(height, Is.GreaterThanOrEqualTo(0f));
                Assert.That(height, Is.LessThanOrEqualTo(1f));
                Assert.That(height, Is.GreaterThanOrEqualTo(previous), "clamped mapping must never wrap");
                previous = height;
            }
        }

        [Test]
        public void ClampToOctaveHeight_NonPositiveWidth_ReturnsZero()
        {
            Assert.That(PitchMath.ClampToOctaveHeight(51f, 45f, 0), Is.EqualTo(0f));
            Assert.That(PitchMath.ClampToOctaveHeight(51f, 45f, -12), Is.EqualTo(0f));
        }

        // ---- note letters -----------------------------------------------------------------

        [TestCase(0, "A")]
        [TestCase(1, "A#")]
        [TestCase(2, "B")]
        [TestCase(3, "C")]
        [TestCase(4, "C#")]
        [TestCase(5, "D")]
        [TestCase(6, "D#")]
        [TestCase(7, "E")]
        [TestCase(8, "F")]
        [TestCase(9, "F#")]
        [TestCase(10, "G")]
        [TestCase(11, "G#")]
        [TestCase(12, "A")]
        public void NoteNameForOffset_CoversAllThirteenOffsets(int offset, string expected)
        {
            Assert.That(PitchMath.NoteNameForOffset(offset), Is.EqualTo(expected));
        }

        [Test]
        public void NoteNameForOffset_OutOfRange_ClampsToEnds()
        {
            Assert.That(PitchMath.NoteNameForOffset(-5), Is.EqualTo("A"));
            Assert.That(PitchMath.NoteNameForOffset(99), Is.EqualTo("A"));
        }

        [Test]
        public void NoteNameForOffset_ReturnsCachedInstance_NoPerCallAllocation()
        {
            for (int offset = 0; offset <= 12; offset++)
            {
                Assert.That(
                    PitchMath.NoteNameForOffset(offset),
                    Is.SameAs(PitchMath.NoteNameForOffset(offset)),
                    "must return an interned literal, not a freshly built string");
            }
        }

        // ---- offset -> height -------------------------------------------------------------

        [Test]
        public void HeightForOffset_Endpoints()
        {
            Assert.That(PitchMath.HeightForOffset(0, 12), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(PitchMath.HeightForOffset(12, 12), Is.EqualTo(1f).Within(1e-6f));
        }

        [TestCase(1, 1f / 12f)]
        [TestCase(3, 0.25f)]
        [TestCase(6, 0.5f)]
        [TestCase(9, 0.75f)]
        [TestCase(11, 11f / 12f)]
        public void HeightForOffset_InteriorOffsets(int offset, float expected)
        {
            Assert.That(PitchMath.HeightForOffset(offset, 12), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void HeightForOffset_ClampsAndGuardsWidth()
        {
            Assert.That(PitchMath.HeightForOffset(-3, 12), Is.EqualTo(0f));
            Assert.That(PitchMath.HeightForOffset(30, 12), Is.EqualTo(1f));
            Assert.That(PitchMath.HeightForOffset(6, 0), Is.EqualTo(0f));
            Assert.That(PitchMath.HeightForOffset(6, -12), Is.EqualTo(0f));
        }

        [Test]
        public void HeightForOffset_AgreesWithClampToOctaveHeight()
        {
            const float floor = 45f;
            for (int offset = 0; offset <= 12; offset++)
            {
                Assert.That(
                    PitchMath.HeightForOffset(offset, 12),
                    Is.EqualTo(PitchMath.ClampToOctaveHeight(floor + offset, floor, 12)).Within(1e-6f));
            }
        }

        // ---- retained wrap helper (unused by gameplay, kept on disk) -----------------------

        [Test]
        public void WrapToOctaveHeight_AtFloor_IsZero()
        {
            Assert.That(PitchMath.WrapToOctaveHeight(48f, 48f, 12), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void WrapToOctaveHeight_HalfOctave_IsHalf()
        {
            Assert.That(PitchMath.WrapToOctaveHeight(54f, 48f, 12), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void WrapToOctaveHeight_FullOctave_WrapsToZero()
        {
            Assert.That(PitchMath.WrapToOctaveHeight(60f, 48f, 12), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(PitchMath.WrapToOctaveHeight(72f, 48f, 12), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void WrapToOctaveHeight_BelowFloor_WrapsToTop()
        {
            Assert.That(PitchMath.WrapToOctaveHeight(47f, 48f, 12), Is.EqualTo(11f / 12f).Within(1e-5f));
            Assert.That(PitchMath.WrapToOctaveHeight(42f, 48f, 12), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(PitchMath.WrapToOctaveHeight(36f, 48f, 12), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(PitchMath.WrapToOctaveHeight(35f, 48f, 12), Is.EqualTo(11f / 12f).Within(1e-5f));
        }

        [Test]
        public void WrapToOctaveHeight_AlwaysInUnitInterval()
        {
            for (float midi = -40f; midi <= 140f; midi += 0.37f)
            {
                float height = PitchMath.WrapToOctaveHeight(midi, 48f, 12);
                Assert.That(height, Is.GreaterThanOrEqualTo(0f));
                Assert.That(height, Is.LessThan(1f));
            }
        }

        [Test]
        public void WrapToOctaveHeight_NonPositiveWidth_ReturnsZero()
        {
            Assert.That(PitchMath.WrapToOctaveHeight(54f, 48f, 0), Is.EqualTo(0f));
            Assert.That(PitchMath.WrapToOctaveHeight(54f, 48f, -12), Is.EqualTo(0f));
        }
    }
}
