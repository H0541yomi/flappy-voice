using FlappyVoice.Gameplay;
using NUnit.Framework;

namespace FlappyVoice.Tests
{
    public class PitchMathTests
    {
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

        // ---- rounding to a real note -------------------------------------------------------

        [TestCase(57f, 57)]
        [TestCase(57.49f, 57)]
        [TestCase(57.5f, 58)]
        [TestCase(56.51f, 57)]
        [TestCase(56.5f, 57)]   // away-from-zero, so .5 rounds up rather than to even
        [TestCase(-0.5f, -1)]
        public void RoundToSemitone_RoundsHalfAwayFromZero(float midi, int expected)
        {
            Assert.That(PitchMath.RoundToSemitone(midi), Is.EqualTo(expected));
        }

        // ---- centred range ------------------------------------------------------------------

        [TestCase(12, 6)]
        [TestCase(11, 5)]   // odd widths keep the floor on a whole semitone
        [TestCase(1, 0)]
        [TestCase(0, 0)]
        [TestCase(-4, 0)]
        public void SemitonesBelowCenter_IsHalfTheWidthRoundedDown(int width, int expected)
        {
            Assert.That(PitchMath.SemitonesBelowCenter(width), Is.EqualTo(expected));
        }

        [TestCase(51f, 45f)]     // D#3 sung -> floor A2
        [TestCase(69f, 63f)]     // A4 sung -> floor D#4
        [TestCase(51.4f, 45f)]   // rounds down to D#3 first
        [TestCase(51.6f, 46f)]   // rounds up to E3 first
        public void CenteredFloorMidi_PutsTheSungNoteInTheMiddle(float sung, float expectedFloor)
        {
            Assert.That(PitchMath.CenteredFloorMidi(sung, 12), Is.EqualTo(expectedFloor).Within(1e-4f));
        }

        [Test]
        public void CenteredFloorMidi_SungNoteLandsAtExactlyHalfHeight()
        {
            for (float midi = 40f; midi <= 80f; midi += 0.25f)
            {
                float floor = PitchMath.CenteredFloorMidi(midi, 12);
                float rounded = PitchMath.RoundToSemitone(midi);
                Assert.That(
                    PitchMath.ClampToOctaveHeight(rounded, floor, 12),
                    Is.EqualTo(0.5f).Within(1e-5f),
                    "the first note must map to mid-screen, sung midi " + midi);
            }
        }

        [Test]
        public void CenteredFloorMidi_IsAlwaysAWholeSemitone()
        {
            for (float midi = -10f; midi <= 120f; midi += 0.31f)
            {
                foreach (int width in new[] { 11, 12, 13 })
                {
                    float floor = PitchMath.CenteredFloorMidi(midi, width);
                    Assert.That(floor, Is.EqualTo(Mathf_Round(floor)).Within(1e-6f),
                        "pipe letters need the floor on a real note, width " + width);
                }
            }
        }

        private static float Mathf_Round(float value)
        {
            return (float)System.Math.Round((double)value);
        }

        // ---- clamped range height -----------------------------------------------------------

        [Test]
        public void ClampToOctaveHeight_AtFloor_IsZero()
        {
            Assert.That(PitchMath.ClampToOctaveHeight(45f, 45f, 12), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ClampToOctaveHeight_HalfRange_IsHalf()
        {
            Assert.That(PitchMath.ClampToOctaveHeight(51f, 45f, 12), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void ClampToOctaveHeight_AtCeiling_IsOne()
        {
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

        // ---- note letters -------------------------------------------------------------------

        [TestCase(60, "C")]
        [TestCase(61, "C#")]
        [TestCase(62, "D")]
        [TestCase(63, "D#")]
        [TestCase(64, "E")]
        [TestCase(65, "F")]
        [TestCase(66, "F#")]
        [TestCase(67, "G")]
        [TestCase(68, "G#")]
        [TestCase(69, "A")]
        [TestCase(70, "A#")]
        [TestCase(71, "B")]
        [TestCase(72, "C")]
        public void NoteNameForMidi_CoversTheChromaticScale(int midi, string expected)
        {
            Assert.That(PitchMath.NoteNameForMidi(midi), Is.EqualTo(expected));
        }

        [Test]
        public void NoteNameForMidi_HandlesNegativeMidi()
        {
            Assert.That(PitchMath.NoteNameForMidi(-1), Is.EqualTo("B"));
            Assert.That(PitchMath.NoteNameForMidi(-12), Is.EqualTo("C"));
        }

        [Test]
        public void NoteNameForMidi_ReturnsCachedInstance_NoPerCallAllocation()
        {
            for (int midi = 36; midi <= 84; midi++)
            {
                Assert.That(
                    PitchMath.NoteNameForMidi(midi),
                    Is.SameAs(PitchMath.NoteNameForMidi(midi)),
                    "must return an interned literal, not a freshly built string");
            }
        }

        [Test]
        public void NoteNameForMidi_SameLetterRepeatsEveryOctave()
        {
            for (int midi = 24; midi <= 96; midi++)
            {
                Assert.That(PitchMath.NoteNameForMidi(midi), Is.SameAs(PitchMath.NoteNameForMidi(midi + 12)));
            }
        }

        // ---- offset -> height ---------------------------------------------------------------

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

        // The spawner places a gap with HeightForOffset and the player reaches it with
        // ClampToOctaveHeight. If these two ever disagree, pipes become unthreadable.
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
    }
}
