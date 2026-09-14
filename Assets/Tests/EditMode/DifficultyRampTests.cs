using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    public class DifficultyRampTests
    {
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

        [Test]
        public void StartsAtZeroAndReachesOneAtTheCap()
        {
            Assert.That(config.DifficultyForPipesPassed(0), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(config.DifficultyForPipesPassed(config.DifficultyRampPipes),
                Is.EqualTo(1f).Within(1e-4f));
        }

        // The point of the cap: a player good enough to pass 500 pipes is not punished with a gap
        // that keeps closing.
        [Test]
        public void HoldsFlatPastTheCap()
        {
            int cap = config.DifficultyRampPipes;
            float atCap = config.DifficultyForPipesPassed(cap);

            Assert.That(config.DifficultyForPipesPassed(cap + 1), Is.EqualTo(atCap).Within(1e-6f));
            Assert.That(config.DifficultyForPipesPassed(cap * 5), Is.EqualTo(atCap).Within(1e-6f));
            Assert.That(config.PipeGapSizeAtDifficulty(config.DifficultyForPipesPassed(cap * 5)),
                Is.EqualTo(config.PipeGapSizeAtDifficulty(atCap)).Within(1e-6f));
        }

        [Test]
        public void NeverGoesBackwardsAsPipesAccumulate()
        {
            float previous = -1f;
            for (int passed = 0; passed <= config.DifficultyRampPipes + 20; passed++)
            {
                float d = config.DifficultyForPipesPassed(passed);
                Assert.That(d, Is.GreaterThanOrEqualTo(previous - 1e-6f), $"went backwards at {passed}");
                Assert.That(d, Is.InRange(0f, 1f));
                previous = d;
            }
        }

        [Test]
        public void NegativeScoreIsTreatedAsTheStartOfARun()
        {
            Assert.That(config.DifficultyForPipesPassed(-5), Is.EqualTo(0f).Within(1e-4f));
        }

        // Both halves of the ramp: the gap tightens and the pipes come faster, on one curve.
        [Test]
        public void SpeedRampsFromThreeToFiveAcrossTheCap()
        {
            Assert.That(config.PipeSpeedAtDifficulty(config.DifficultyForPipesPassed(0)),
                Is.EqualTo(3f).Within(1e-4f));
            Assert.That(
                config.PipeSpeedAtDifficulty(config.DifficultyForPipesPassed(config.DifficultyRampPipes)),
                Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void SpeedNeverGoesBackwardsAndHoldsFlatPastTheCap()
        {
            float previous = -1f;
            for (int passed = 0; passed <= config.DifficultyRampPipes; passed++)
            {
                float speed = config.PipeSpeedAtDifficulty(config.DifficultyForPipesPassed(passed));
                Assert.That(speed, Is.GreaterThanOrEqualTo(previous - 1e-6f), $"slowed down at {passed}");
                Assert.That(speed, Is.InRange(config.PipeSpeed, config.MaxPipeSpeed));
                previous = speed;
            }

            float atCap = config.PipeSpeedAtDifficulty(config.DifficultyForPipesPassed(config.DifficultyRampPipes));
            Assert.That(config.PipeSpeedAtDifficulty(config.DifficultyForPipesPassed(config.DifficultyRampPipes * 5)),
                Is.EqualTo(atCap).Within(1e-6f));
        }

        // Spacing is deliberately NOT on the ramp: pipes come sooner, they never bunch up. The
        // spawner paces itself by distance, so this is the whole of what speed does to the layout.
        [Test]
        public void SpacingIsUntouchedByTheRampAndOnlyTheIntervalShortens()
        {
            float startSpeed = config.PipeSpeedAtDifficulty(0f);
            float cappedSpeed = config.PipeSpeedAtDifficulty(1f);

            float startInterval = config.SpawnIntervalSecAtSpeed(startSpeed);
            float cappedInterval = config.SpawnIntervalSecAtSpeed(cappedSpeed);

            Assert.That(startInterval * startSpeed, Is.EqualTo(config.PipeSpacingUnits).Within(1e-4f));
            Assert.That(cappedInterval * cappedSpeed, Is.EqualTo(config.PipeSpacingUnits).Within(1e-4f));
            Assert.That(cappedInterval, Is.LessThan(startInterval));
        }

        // The invincibility floor is a fixed number of seconds; the gap between pipes is not. At
        // full speed it must still be impossible for one hit to carry the bird through the next pipe.
        [Test]
        public void InvincibilityFloorStaysUnderTheShortestGapBetweenPipes()
        {
            float shortestInterval = config.SpawnIntervalSecAtSpeed(config.PipeSpeedAtDifficulty(1f));

            Assert.That(config.MinInvincibleSec, Is.LessThan(shortestInterval),
                $"a hit at full speed buys a free pass: floor {config.MinInvincibleSec}s, "
                + $"pipes {shortestInterval}s apart");
        }

        // The margin has to actually shrink, or the ramp is decorative.
        [Test]
        public void MarginNarrowsWithPipesPassed()
        {
            float start = config.PipeGapSizeAtDifficulty(config.DifficultyForPipesPassed(0));
            float half = config.PipeGapSizeAtDifficulty(
                config.DifficultyForPipesPassed(config.DifficultyRampPipes / 2));
            float capped = config.PipeGapSizeAtDifficulty(
                config.DifficultyForPipesPassed(config.DifficultyRampPipes));

            Assert.That(half, Is.LessThan(start));
            Assert.That(capped, Is.LessThan(half));
        }

        // How much room is left before the NEIGHBOURING note would start clearing the gap. The bird
        // radius and the gap size are both tunable and both move this; if it ever goes negative the
        // gap has quietly become a two-note gap and the game got easier, not harder.
        [Test]
        public void NeighbourNoteHeadroomSurvivesTheWidestGap()
        {
            float gap = config.PipeGapSizeAtDifficulty(0f);
            float reach = gap * 0.5f - config.PlayerBodyRadiusUnits;
            float neighbourDistance = config.UnitsPerSemitone;

            Assert.That(reach, Is.LessThan(neighbourDistance),
                $"the next note up clears the gap: reach {reach}, neighbour at {neighbourDistance}");
            Assert.That(reach, Is.GreaterThan(0f),
                "the bird itself no longer clears the gap");
        }

        // The invariant the whole ramp has to respect: at its very tightest the gap still admits
        // the note it was placed on, so a fully ramped pipe is always passable by singing.
        [Test]
        public void FullyRampedGapStillAdmitsTheNoteItWasPlacedOn()
        {
            const int gapOffset = 8;
            float difficulty = config.DifficultyForPipesPassed(config.DifficultyRampPipes * 10);
            float gap = config.PipeGapSizeAtDifficulty(difficulty);
            float gapCenterY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                PitchMath.HeightForOffset(gapOffset, config.OctaveWidthSemitones));

            float noteY = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY,
                PitchMath.HeightForOffset(gapOffset, config.OctaveWidthSemitones));
            float half = gap * 0.5f - BirdRadius;

            Assert.That(Mathf.Abs(noteY - gapCenterY), Is.LessThanOrEqualTo(half),
                "the gap note no longer fits once the ramp is capped");
        }

        private float BirdRadius => config.PlayerBodyRadiusUnits;
    }
}
