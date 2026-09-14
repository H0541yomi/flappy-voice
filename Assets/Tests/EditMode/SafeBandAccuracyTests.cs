using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using FlappyVoice.UI;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    // The green band on the tuner is a promise: sing inside it and you clear the gap. It is drawn
    // from the safe pitch window, which is computed from the gap size and the bird's radius - so
    // changing either the ramp or the bird has to leave the band still telling the truth.
    public class SafeBandAccuracyTests
    {
        private const float FloorMidi = 48f;
        private GameConfig config;

        [SetUp]
        public void SetUp()
        {
            config = GameConfig.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        private bool Window(int gapOffset, float difficulty, out float lowMidi, out float highMidi)
        {
            float gap = config.PipeGapSizeAtDifficulty(difficulty);
            float gapCenterY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                PitchMath.HeightForOffset(gapOffset, config.OctaveWidthSemitones));

            return PitchMath.TrySafePitchWindow(gapCenterY, gap, config.PlayerBodyRadiusUnits,
                config.PlayfieldMinY, config.PlayfieldMaxY, FloorMidi,
                config.OctaveWidthSemitones, out lowMidi, out highMidi);
        }

        // Offsets kept well inside the range: at the very top and bottom the window deliberately
        // widens, because a pitch past the ceiling parks the bird on the ceiling.
        [TestCase(6, 0f)]
        [TestCase(6, 1f)]
        [TestCase(12, 0f)]
        [TestCase(12, 1f)]
        [TestCase(17, 0f)]
        [TestCase(17, 1f)]
        public void BandCoversExactlyTheNoteTheGapWasPlacedOn(int gapOffset, float difficulty)
        {
            Assert.That(Window(gapOffset, difficulty, out float low, out float high), Is.True);

            Assert.That(FloorMidi + gapOffset, Is.InRange(low, high), "the note the gap is on");

            Assert.That(FloorMidi + gapOffset - 1, Is.Not.InRange(low, high),
                "the note below is highlighted as safe but does not clear the gap");
            Assert.That(FloorMidi + gapOffset + 1, Is.Not.InRange(low, high),
                "the note above is highlighted as safe but does not clear the gap");
        }

        // The band is positioned and sized in pixels off the same ruler the tick marks use, so a
        // window in semitones has to come out as a sane number of pixels on that ruler.
        [Test]
        public void BandWidthInPixelsMatchesTheDialRuler()
        {
            Assert.That(Window(12, 0f, out float low, out float high), Is.True);

            float widthPx = (high - low) * TunerBarUI.PixelsPerSemitone;
            float oneSemitonePx = 1f * TunerBarUI.PixelsPerSemitone;
            float twoSemitonePx = 2f * TunerBarUI.PixelsPerSemitone;

            Assert.That(widthPx, Is.GreaterThan(oneSemitonePx),
                "the band is too narrow to aim at on the ruler");
            Assert.That(widthPx, Is.LessThan(twoSemitonePx),
                "the band is wide enough to cover a neighbouring note");
        }

        // Tightening the ramp must tighten the band with it, or the tuner keeps promising the
        // room the player had a hundred pipes ago.
        [Test]
        public void BandNarrowsAsTheRunRamps()
        {
            Assert.That(Window(12, 0f, out float lowStart, out float highStart), Is.True);
            Assert.That(Window(12, 1f, out float lowEnd, out float highEnd), Is.True);

            Assert.That(highEnd - lowEnd, Is.LessThan(highStart - lowStart));
        }

        // The bird's radius is subtracted from the opening, so a bigger bird has to mean a
        // narrower promise. This is the coupling that made the band worth a test of its own.
        [Test]
        public void BandNarrowsAsTheBirdGrows()
        {
            float gap = config.PipeGapSizeAtDifficulty(0f);
            float gapCenterY = 0f;
            float small = config.PlayerBodyRadiusUnits * 0.5f;

            PitchMath.TrySafePitchWindow(gapCenterY, gap, small, config.PlayfieldMinY,
                config.PlayfieldMaxY, FloorMidi, config.OctaveWidthSemitones,
                out float lowSmall, out float highSmall);
            PitchMath.TrySafePitchWindow(gapCenterY, gap, config.PlayerBodyRadiusUnits,
                config.PlayfieldMinY, config.PlayfieldMaxY, FloorMidi, config.OctaveWidthSemitones,
                out float lowReal, out float highReal);

            Assert.That(highReal - lowReal, Is.LessThan(highSmall - lowSmall));
        }
    }
}
