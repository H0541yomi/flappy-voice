using System;
using FlappyVoice.Audio;
using NUnit.Framework;

namespace FlappyVoice.Tests
{
    public class YinPitchDetectorTests
    {
        private const int SampleRate = 44100;
        private const int BufferSize = 2048;

        private static YinPitchDetector MakeDetector()
        {
            return new YinPitchDetector(BufferSize, SampleRate, 70f, 1200f, 0.15f);
        }

        private static float[] MakeSine(float hz)
        {
            var buffer = new float[BufferSize];
            for (int i = 0; i < BufferSize; i++)
            {
                buffer[i] = 0.5f * (float)Math.Sin(2.0 * Math.PI * hz * i / SampleRate);
            }
            return buffer;
        }

        private static float[] MakeHarmonicStack(float fundamentalHz, int harmonics)
        {
            var buffer = new float[BufferSize];
            for (int i = 0; i < BufferSize; i++)
            {
                double v = 0.0;
                for (int k = 1; k <= harmonics; k++)
                {
                    v += Math.Sin(2.0 * Math.PI * fundamentalHz * k * i / SampleRate) / k;
                }
                buffer[i] = (float)(v * 0.3);
            }
            return buffer;
        }

        [TestCase(82.41f)]
        [TestCase(110f)]
        [TestCase(220f)]
        [TestCase(440f)]
        [TestCase(523.25f)]
        public void Detect_Sine_ReturnsFundamentalWithinOnePercent(float hz)
        {
            var detector = MakeDetector();
            float confidence;
            float detected = detector.Detect(MakeSine(hz), 0, BufferSize, out confidence);

            Assert.That(detected, Is.EqualTo(hz).Within(hz * 0.01f));
            Assert.That(confidence, Is.GreaterThan(0.8f));
        }

        [TestCase(110f)]
        [TestCase(196f)]
        [TestCase(220f)]
        public void Detect_HarmonicStack_ReturnsFundamentalNotAnOctaveAway(float fundamentalHz)
        {
            var detector = MakeDetector();
            float confidence;
            float detected = detector.Detect(MakeHarmonicStack(fundamentalHz, 8), 0, BufferSize, out confidence);

            Assert.That(detected, Is.EqualTo(fundamentalHz).Within(fundamentalHz * 0.01f));
            Assert.That(Math.Abs(detected - fundamentalHz * 2f), Is.GreaterThan(fundamentalHz * 0.5f), "octave-up error");
            Assert.That(Math.Abs(detected - fundamentalHz * 0.5f), Is.GreaterThan(fundamentalHz * 0.25f), "octave-down error");
        }

        [Test]
        public void Detect_WhiteNoise_ReturnsZero()
        {
            var detector = MakeDetector();
            var random = new Random(1234);
            var buffer = new float[BufferSize];
            for (int i = 0; i < BufferSize; i++) buffer[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            float confidence;
            float detected = detector.Detect(buffer, 0, BufferSize, out confidence);

            Assert.That(detected, Is.EqualTo(0f));
            Assert.That(confidence, Is.EqualTo(0f));
        }

        [Test]
        public void Detect_Silence_ReturnsZero()
        {
            var detector = MakeDetector();
            float confidence;
            float detected = detector.Detect(new float[BufferSize], 0, BufferSize, out confidence);

            Assert.That(detected, Is.EqualTo(0f));
            Assert.That(confidence, Is.EqualTo(0f));
        }

        [Test]
        public void Detect_RepeatedCalls_AreStable()
        {
            var detector = MakeDetector();
            var buffer = MakeSine(220f);

            float confidence;
            float first = detector.Detect(buffer, 0, BufferSize, out confidence);
            for (int i = 0; i < 100; i++)
            {
                float c;
                float again = detector.Detect(buffer, 0, BufferSize, out c);
                Assert.That(again, Is.EqualTo(first).Within(1e-4f));
                Assert.That(c, Is.EqualTo(confidence).Within(1e-4f));
            }
        }

        [Test]
        public void Detect_RejectsOutOfRangeArguments()
        {
            var detector = MakeDetector();
            var buffer = MakeSine(220f);
            float confidence;

            Assert.That(detector.Detect(null, 0, BufferSize, out confidence), Is.EqualTo(0f));
            Assert.That(detector.Detect(buffer, -1, BufferSize, out confidence), Is.EqualTo(0f));
            Assert.That(detector.Detect(buffer, 0, BufferSize + 1, out confidence), Is.EqualTo(0f));
            Assert.That(detector.Detect(buffer, 0, 0, out confidence), Is.EqualTo(0f));
        }

        [Test]
        public void ComputeRms_MatchesKnownValues()
        {
            Assert.That(YinPitchDetector.ComputeRms(new float[16], 0, 16), Is.EqualTo(0f).Within(1e-6f));

            var constant = new float[16];
            for (int i = 0; i < constant.Length; i++) constant[i] = 0.5f;
            Assert.That(YinPitchDetector.ComputeRms(constant, 0, 16), Is.EqualTo(0.5f).Within(1e-6f));

            // RMS of a full-scale sine is 1/sqrt(2)
            Assert.That(YinPitchDetector.ComputeRms(MakeSine(441f), 0, BufferSize),
                Is.EqualTo(0.5f / (float)Math.Sqrt(2.0)).Within(0.01f));
        }
    }
}
