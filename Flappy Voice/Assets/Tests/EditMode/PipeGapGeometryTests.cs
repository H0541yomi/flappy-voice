using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    // A pipe gap is specified in NOTES: it spans two adjacent notes and rejects the notes either
    // side of that pair. Nothing in the running game states that in one place - it falls out of the
    // gap height, the pitch-to-height mapping and the bird's collider together - so it is pinned
    // here, at both ends of the difficulty ramp.
    public class PipeGapGeometryTests
    {
        // Authored on the player's CircleCollider2D in SceneBuilder.BuildPlayer.
        private const float BirdRadius = 0.42f;

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
        public void GapIsSpecifiedInNotes()
        {
            Assert.That(config.PipeGapNotes, Is.EqualTo(2));
        }

        [Test]
        public void GapHeightTracksTheWidthOfTheRange()
        {
            float wide = config.PipeGapSizeAtDifficulty(0f);

            // Same gap in notes over twice the semitones: a semitone is half as tall on screen, so
            // the note span inside the gap halves while the bird's own clearance does not move.
            GameConfig narrower = GameConfig.CreateDefault();
            try
            {
                float oneOctave = SizeAtOctaveWidth(narrower, 12);
                Assert.That(wide, Is.LessThan(oneOctave));
                Assert.That(oneOctave - wide,
                    Is.EqualTo(config.UnitsPerSemitone).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(narrower);
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void BothNotesOfThePairFitThroughTheGap(float difficulty)
        {
            AssertPasses(difficulty, 0, true);
            AssertPasses(difficulty, 1, true);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void NotesOutsideThePairDoNotFit(float difficulty)
        {
            AssertPasses(difficulty, -1, false);
            AssertPasses(difficulty, 2, false);
        }

        // The gap never narrows past its own promise: the ramped-down clearance still has to admit
        // the pair, or the difficulty curve would quietly turn a two-note gap into a one-note gap.
        [Test]
        public void ClearanceRampNeverDropsBelowTheNoteSpan()
        {
            Assert.That(config.MinPipeGapClearanceUnits, Is.LessThan(config.PipeGapClearanceUnits));
            Assert.That(config.PipeGapSizeAtDifficulty(1f),
                Is.LessThan(config.PipeGapSizeAtDifficulty(0f)));
        }

        // noteFromLowerOfPair 0 is the lower note the gap is named for, 1 the upper one; -1 and 2
        // are the neighbours that must be shut out.
        private void AssertPasses(float difficulty, int noteFromLowerOfPair, bool expected)
        {
            const int lowerOffset = 8;
            float gap = config.PipeGapSizeAtDifficulty(difficulty);
            float gapCenterY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                PitchMath.HeightForNotePair(lowerOffset, config.OctaveWidthSemitones));

            float noteY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                PitchMath.HeightForOffset(lowerOffset + noteFromLowerOfPair, config.OctaveWidthSemitones));

            Assert.That(Fits(gap, gapCenterY, noteY), Is.EqualTo(expected),
                $"note {noteFromLowerOfPair} of the pair, difficulty {difficulty}, gap {gap}");
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

            for (int lowerOffset = 0; lowerOffset < width; lowerOffset++)
            {
                float gapHeight = PitchMath.HeightForNotePair(lowerOffset, width);
                float floor = PitchMath.FloorMidiForNoteAtHeight(sungMidi, gapHeight, width);

                float gapCenterY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY, gapHeight);
                float birdY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                    PitchMath.ClampToOctaveHeight(sungMidi, floor, width));

                Assert.That(Fits(gap, gapCenterY, birdY), Is.True,
                    $"gap at pair {lowerOffset} of {width}, sung {sungMidi}");
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
