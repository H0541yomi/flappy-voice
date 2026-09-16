using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    // A pipe gap is specified in NOTES: it is centred ON one note, admits that note, and rejects
    // the notes either side of it. Nothing in the running game states that in one place - it falls
    // out of the gap height, the pitch-to-height mapping and the bird's collider together - so it
    // is pinned here, at both ends of the difficulty ramp.
    public class PipeGapGeometryTests
    {
        // Authored on the player's CircleCollider2D in SceneBuilder.BuildPlayer.
        // Read from the config rather than pinned here: the assertions below ARE the invariant,
        // so a radius change has to keep satisfying them rather than quietly retuning the test.
        private float BirdRadius => config.PlayerBodyRadiusUnits;

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

        [Test]
        public void DefaultRangeIsTwoOctaves()
        {
            Assert.That(config.OctaveWidthSemitones, Is.EqualTo(24));
        }

        [Test]
        public void GapIsOneNoteWide()
        {
            Assert.That(config.PipeGapNotes, Is.EqualTo(1));
        }

        // A one-note gap is pure clearance - there is no note span left inside it to scale - so
        // the range width no longer moves the opening. What the range width does move is the
        // neighbouring note, and that is the bound the gap has to stay under.
        [Test]
        public void GapIsTheClearanceAndNothingElse()
        {
            Assert.That(config.PipeGapSizeAtDifficulty(0f),
                Is.EqualTo(config.PipeGapClearanceUnits).Within(1e-4f));
            Assert.That(config.PipeGapSizeAtDifficulty(1f),
                Is.EqualTo(config.MinPipeGapClearanceUnits).Within(1e-4f));

            GameConfig narrower = GameConfig.CreateDefault();
            try
            {
                Assert.That(SizeAtOctaveWidth(narrower, 12),
                    Is.EqualTo(config.PipeGapSizeAtDifficulty(0f)).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(narrower);
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void TheNoteTheGapWasPlacedOnFitsThroughIt(float difficulty)
        {
            AssertPasses(difficulty, 0, true);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void NeitherNeighbourOfThatNoteFits(float difficulty)
        {
            AssertPasses(difficulty, -1, false);
            AssertPasses(difficulty, 1, false);
        }

        // The gap never narrows past its own promise: the ramped-down clearance still has to admit
        // the note it is placed on, or the difficulty curve would close the pipe outright.
        [Test]
        public void ClearanceRampNeverDropsBelowTheBird()
        {
            Assert.That(config.MinPipeGapClearanceUnits, Is.LessThan(config.PipeGapClearanceUnits));
            Assert.That(config.PipeGapSizeAtDifficulty(1f),
                Is.LessThan(config.PipeGapSizeAtDifficulty(0f)));
            Assert.That(config.PipeGapSizeAtDifficulty(1f),
                Is.GreaterThan(2f * config.PlayerBodyRadiusUnits));
        }

        // semitonesFromGapNote 0 is the note the gap is centred on; -1 and +1 are the neighbours
        // that must be shut out.
        private void AssertPasses(float difficulty, int semitonesFromGapNote, bool expected)
        {
            const int gapOffset = 8;
            float gap = config.PipeGapSizeAtDifficulty(difficulty);
            float gapCenterY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                PitchMath.HeightForOffset(gapOffset, config.OctaveWidthSemitones));

            float noteY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                PitchMath.HeightForOffset(gapOffset + semitonesFromGapNote, config.OctaveWidthSemitones));

            Assert.That(Fits(gap, gapCenterY, noteY), Is.EqualTo(expected),
                $"note {semitonesFromGapNote} from the gap note, difficulty {difficulty}, gap {gap}");
        }

        // Bird held at noteY against a gap of `gap` centred on gapCenterY.
        private bool Fits(float gap, float gapCenterY, float noteY)
        {
            return noteY + BirdRadius <= gapCenterY + (gap * 0.5f)
                && noteY - BirdRadius >= gapCenterY - (gap * 0.5f);
        }

        // End to end on the thing the anchor promises: whatever note the player sings to start the
        // run is placed at the height of the gap ahead, so that first pipe is threaded by holding
        // the note - from any pipe height, and for a sung pitch that is nowhere near a semitone.
        [TestCase(60f)]
        [TestCase(48.4f)]
        [TestCase(67.6f)]
        public void FirstSungNoteThreadsTheGapItWasAnchoredAt(float sungMidi)
        {
            int width = config.OctaveWidthSemitones;
            float gap = config.PipeGapSizeAtDifficulty(0f);

            for (int gapOffset = 0; gapOffset <= width; gapOffset++)
            {
                float gapHeight = PitchMath.HeightForOffset(gapOffset, width);
                float floor = PitchMath.FloorMidiForNoteAtHeight(sungMidi, gapHeight, width);

                float gapCenterY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY, gapHeight);
                float birdY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                    PitchMath.ClampToOctaveHeight(sungMidi, floor, width));

                Assert.That(Fits(gap, gapCenterY, birdY), Is.True,
                    $"gap at note {gapOffset} of {width}, sung {sungMidi}");
            }
        }

        // The letter drawn in a gap is an instruction, so it has to name the note that actually
        // threads that gap. The two are reached by different routes - the label counts semitones
        // up from the floor, the geometry maps a height back to a pitch - and this pins that they
        // land on the same note, and that holding it really does clear the pipe.
        [TestCase(60f)]
        [TestCase(48.4f)]
        [TestCase(67.6f)]
        public void GapLetterNamesTheNoteThatThreadsIt(float sungMidi)
        {
            int width = config.OctaveWidthSemitones;
            float gap = config.PipeGapSizeAtDifficulty(0f);

            for (int gapOffset = 0; gapOffset <= width; gapOffset++)
            {
                float gapHeight = PitchMath.HeightForOffset(gapOffset, width);
                float floor = PitchMath.FloorMidiForNoteAtHeight(sungMidi, gapHeight, width);

                // The pitch whose height IS this gap's centre, read back out of the mapping the
                // bird flies by rather than added up the way the label is.
                float threadingMidi = floor + (gapHeight * width);

                Assert.That(PitchMath.NoteNameForOffset(floor, gapOffset),
                    Is.EqualTo(PitchMath.NoteNameForMidi(PitchMath.RoundToSemitone(threadingMidi))),
                    $"gap at note {gapOffset} of {width}, floor {floor}");

                float gapCenterY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY, gapHeight);
                float birdY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                    PitchMath.ClampToOctaveHeight(threadingMidi, floor, width));

                Assert.That(Fits(gap, gapCenterY, birdY), Is.True,
                    $"note named on gap {gapOffset} does not clear it");
            }
        }

        private static float SizeAtOctaveWidth(GameConfig target, int width)
        {
            SetPrivateInt(target, "_octaveWidthSemitones", width);
            return target.PipeGapSizeAtDifficulty(0f);
        }

        private static void SetPrivateInt(GameConfig target, string field, int value)
        {
            typeof(GameConfig)
                .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
