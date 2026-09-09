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
