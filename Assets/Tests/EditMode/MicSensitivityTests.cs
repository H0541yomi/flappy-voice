using FlappyVoice.Audio;
using FlappyVoice.Config;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    /// <summary>
    /// Pins the pause menu's sensitivity slider to the amplitude gate it drives. The gate is the
    /// one number that decides what counts as singing, so the mapping has to stay bounded and its
    /// resting position has to stay the tuning the game shipped with.
    /// </summary>
    public class MicSensitivityTests
    {
        [Test]
        public void SliderEndsAreTheGateEnds()
        {
            Assert.That(PitchTracker.GateRmsForSensitivity(1f),
                Is.EqualTo(PitchTracker.MostSensitiveGateRms).Within(1e-6f));
            Assert.That(PitchTracker.GateRmsForSensitivity(0f),
                Is.EqualTo(PitchTracker.LeastSensitiveGateRms).Within(1e-6f));
        }

        // The slider has to start where the game already was. A default that drifted off the
        // authored gate would retune detection for every player who never opens the pause menu.
        [Test]
        public void DefaultSensitivityReproducesTheAuthoredGate()
        {
            GameConfig config = GameConfig.CreateDefault();
            float gate = PitchTracker.GateRmsForSensitivity(PitchTracker.DefaultSensitivity01);

            Assert.That(gate, Is.EqualTo(config.AmplitudeGateRms).Within(config.AmplitudeGateRms * 0.05f));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void MoreSensitiveMeansALowerGate()
        {
            Assert.That(PitchTracker.GateRmsForSensitivity(0.8f),
                Is.LessThan(PitchTracker.GateRmsForSensitivity(0.2f)));
        }

        // Clamped, not extrapolated: a slider is 0..1 but nothing stops a stored value from
        // being out of range, and a negative gate would make silence itself voiced.
        [TestCase(-1f)]
        [TestCase(2f)]
        public void OutOfRangeSensitivityStaysInsideTheGateRange(float sensitivity)
        {
            float gate = PitchTracker.GateRmsForSensitivity(sensitivity);

            Assert.That(gate, Is.InRange(PitchTracker.MostSensitiveGateRms,
                PitchTracker.LeastSensitiveGateRms));
        }
    }
}
