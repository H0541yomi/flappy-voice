using FlappyVoice.Gameplay;
using NUnit.Framework;
using UnityEngine;

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

        // The gap sits on the boundary between the two notes it is named for, so it is exactly
        // half a semitone above the lower one - that is what gives both notes the same margin.
        [Test]
        public void HeightForNotePair_SitsBetweenItsTwoNotes()
        {
            for (int offset = 0; offset < 24; offset++)
            {
                float pair = PitchMath.HeightForNotePair(offset, 24);
                float lower = PitchMath.HeightForOffset(offset, 24);
                float upper = PitchMath.HeightForOffset(offset + 1, 24);

                Assert.That(pair, Is.EqualTo((lower + upper) * 0.5f).Within(1e-6f));
            }
        }

        // Never 0 or 1: an edge-pinned gap centre puts half the opening off screen.
        [Test]
        public void HeightForNotePair_StaysInsideThePlayfield()
        {
            Assert.That(PitchMath.HeightForNotePair(0, 24), Is.GreaterThan(0f));
            Assert.That(PitchMath.HeightForNotePair(23, 24), Is.LessThan(1f));
            Assert.That(PitchMath.HeightForNotePair(6, 0), Is.EqualTo(0f));
        }

        [TestCase(69f, 0f)]
        [TestCase(69.25f, 25f)]
        [TestCase(68.6f, -40f)]
        // Dead on the boundary the shared rounding goes up, so the deviation reads as flat of the
        // note above rather than sharp of the note below. Either is true; only one is shown.
        [TestCase(69.5f, -50f)]
        public void CentsFromNearestSemitone_IsSignedAndWithinHalfASemitone(float midi, float expected)
        {
            Assert.That(PitchMath.CentsFromNearestSemitone(midi), Is.EqualTo(expected).Within(1e-3f));
        }
        // ---- anchoring a note at a chosen height ----------------------------------------------

        [TestCase(0f, 0)]
        [TestCase(0.5f, 12)]
        [TestCase(1f, 24)]
        [TestCase(0.25f, 6)]
        public void SemitonesBelowHeight_IsTheHeightInSemitones(float height, int expected)
        {
            Assert.That(PitchMath.SemitonesBelowHeight(height, 24), Is.EqualTo(expected));
        }

        // The note has to stay inside the range it defines, whatever height is asked for.
        [Test]
        public void SemitonesBelowHeight_ClampsAndGuardsWidth()
        {
            Assert.That(PitchMath.SemitonesBelowHeight(-2f, 24), Is.EqualTo(0));
            Assert.That(PitchMath.SemitonesBelowHeight(3f, 24), Is.EqualTo(24));
            Assert.That(PitchMath.SemitonesBelowHeight(0.5f, 0), Is.EqualTo(0));
            Assert.That(PitchMath.SemitonesBelowHeight(0.5f, -24), Is.EqualTo(0));
        }

        // Anchoring at half height is the old centred behaviour, so the two agree there and the
        // change of rule cannot have moved anything the dev anchor relies on.
        [Test]
        public void FloorMidiForNoteAtHeight_AtHalfHeightMatchesTheCentredFloor()
        {
            for (float midi = 40f; midi <= 80f; midi += 1f)
            {
                Assert.That(PitchMath.FloorMidiForNoteAtHeight(midi, 0.5f, 24),
                    Is.EqualTo(PitchMath.CenteredFloorMidi(midi, 24)).Within(1e-4f));
            }
        }

        // The whole point: the sung note comes out at the height it was aimed at, to the semitone.
        [Test]
        public void FloorMidiForNoteAtHeight_PutsTheNoteAtTheRequestedHeight()
        {
            const int width = 24;
            for (int offset = 0; offset <= width; offset++)
            {
                float requested = offset / (float)width;
                float floor = PitchMath.FloorMidiForNoteAtHeight(57f, requested, width);

                Assert.That(PitchMath.ClampToOctaveHeight(57f, floor, width),
                    Is.EqualTo(requested).Within(1e-5f));
            }
        }

        // Pipe letters are named off the floor, so a floor between two semitones would name notes
        // nobody can sing.
        [Test]
        public void FloorMidiForNoteAtHeight_IsAlwaysAWholeSemitone()
        {
            for (float height = 0f; height <= 1f; height += 0.017f)
            {
                float floor = PitchMath.FloorMidiForNoteAtHeight(60.4f, height, 24);
                Assert.That(floor, Is.EqualTo(Mathf.Round(floor)).Within(1e-4f));
            }
        }
        // ---- which pitches clear a gap ---------------------------------------------------------

        private const float MinY = -4.5f;
        private const float MaxY = 4.5f;
        private const int Width = 24;
        private const float BirdRadius = 0.42f;

        private static float YForMidi(float midi, float floorMidi)
        {
            return Mathf.Lerp(MinY, MaxY, PitchMath.ClampToOctaveHeight(midi, floorMidi, Width));
        }

        // The edge of the safe band is the pitch that just barely misses the pipe: the bird's rim
        // lands exactly on the rim of the opening. Placed so that edge falls on A4 = 440 Hz.
        [Test]
        public void TrySafePitchWindow_EdgeIsThePitchThatJustClearsTheWall()
        {
            const float floor = 57f;
            const float gap = 1.75f;
            const float gapCenterY = -0.455f;

            Assert.That(PitchMath.TrySafePitchWindow(gapCenterY, gap, BirdRadius, MinY, MaxY, floor,
                Width, out float lowMidi, out float highMidi), Is.True);

            Assert.That(highMidi, Is.EqualTo(PitchMath.A4Midi).Within(1e-3f));
            Assert.That(PitchMath.MidiToHz(highMidi), Is.EqualTo(440f).Within(0.5f));

            // Bird singing that edge pitch: its top rim is on the gap's top rim, to the millimetre.
            Assert.That(YForMidi(highMidi, floor) + BirdRadius,
                Is.EqualTo(gapCenterY + (gap * 0.5f)).Within(1e-3f));

            // A hair sharp of it is a crash; a hair flat still gets through.
            Assert.That(YForMidi(highMidi + 0.05f, floor) + BirdRadius,
                Is.GreaterThan(gapCenterY + (gap * 0.5f)));
            Assert.That(YForMidi(highMidi - 0.05f, floor) + BirdRadius,
                Is.LessThan(gapCenterY + (gap * 0.5f)));

            Assert.That(YForMidi(lowMidi, floor) - BirdRadius,
                Is.EqualTo(gapCenterY - (gap * 0.5f)).Within(1e-3f));
        }

        // Every pitch inside the band clears the gap, and nothing outside it does.
        [Test]
        public void TrySafePitchWindow_AgreesWithTheGeometryAcrossTheBand()
        {
            const float floor = 50f;
            const float gap = 1.75f;
            const float gapCenterY = 0.6f;

            Assert.That(PitchMath.TrySafePitchWindow(gapCenterY, gap, BirdRadius, MinY, MaxY, floor,
                Width, out float lowMidi, out float highMidi), Is.True);

            for (float midi = floor; midi <= floor + Width; midi += 0.05f)
            {
                float y = YForMidi(midi, floor);
                bool clears = y + BirdRadius <= gapCenterY + (gap * 0.5f) + 1e-4f
                    && y - BirdRadius >= gapCenterY - (gap * 0.5f) - 1e-4f;
                bool inBand = midi >= lowMidi - 1e-4f && midi <= highMidi + 1e-4f;

                Assert.That(inBand, Is.EqualTo(clears), $"midi {midi}");
            }
        }

        // A gap right at the ceiling is cleared by holding the top note - and by anything above it,
        // because the bird cannot go higher than the ceiling. The band has to say so.
        [Test]
        public void TrySafePitchWindow_ExtendsPastTheRangeWhereTheBirdIsPinned()
        {
            const float floor = 50f;

            Assert.That(PitchMath.TrySafePitchWindow(MaxY, 1.75f, BirdRadius, MinY, MaxY, floor,
                Width, out _, out float highMidi), Is.True);
            Assert.That(highMidi, Is.GreaterThan(floor + Width));

            Assert.That(PitchMath.TrySafePitchWindow(MinY, 1.75f, BirdRadius, MinY, MaxY, floor,
                Width, out float lowMidi, out _), Is.True);
            Assert.That(lowMidi, Is.LessThan(floor));
        }

        [Test]
        public void TrySafePitchWindow_FalseWhenNothingCanClearTheGap()
        {
            const float floor = 50f;

            // Opening narrower than the bird.
            Assert.That(PitchMath.TrySafePitchWindow(0f, BirdRadius, BirdRadius, MinY, MaxY, floor,
                Width, out _, out _), Is.False);

            // Degenerate playfield or range.
            Assert.That(PitchMath.TrySafePitchWindow(0f, 1.75f, BirdRadius, 0f, 0f, floor, Width,
                out _, out _), Is.False);
            Assert.That(PitchMath.TrySafePitchWindow(0f, 1.75f, BirdRadius, MinY, MaxY, floor, 0,
                out _, out _), Is.False);
        }
        // ---- height follows pitch, not the nearest note ----------------------------------------

        // 450 Hz is A4 plus 39 cents. The bird belongs 39 cents of a semitone above A, not on it:
        // the mapping is continuous in pitch and nothing in it rounds to a note.
        [Test]
        public void ClampToOctaveHeight_DoesNotSnapToTheNearestNote()
        {
            const float floor = 57f;
            float midi = PitchMath.HzToMidi(450f);
            float height = PitchMath.ClampToOctaveHeight(midi, floor, Width);

            float snapped = PitchMath.ClampToOctaveHeight(PitchMath.RoundToSemitone(midi), floor, Width);
            Assert.That(height, Is.EqualTo((midi - floor) / Width).Within(1e-6f));
            Assert.That(height, Is.Not.EqualTo(snapped).Within(1e-4f));
            Assert.That(height - snapped, Is.EqualTo(0.39f / Width).Within(2e-3f));
        }

        // A cent of extra pitch is a cent of extra height, everywhere inside the range: no plateaus
        // to sit on, which is what a note-quantised mapping would leave behind.
        [Test]
        public void ClampToOctaveHeight_IsStrictlyMonotonicInPitch()
        {
            const float floor = 45f;
            float previous = -1f;

            for (float cents = 1f; cents < Width * 100f; cents += 1f)
            {
                float height = PitchMath.ClampToOctaveHeight(floor + (cents / 100f), floor, Width);
                Assert.That(height, Is.GreaterThan(previous), $"{cents} cents above the floor");
                previous = height;
            }
        }
    }
}