using System;

namespace FlappyVoice.Audio
{
    public sealed class YinPitchDetector
    {
        private const float UnvoicedCeiling = 0.5f;

        private readonly float[] _difference;
        private readonly float[] _cmndf;
        private readonly int _tauMin;
        private readonly int _tauMax;
        private readonly float _threshold;

        public int BufferSize { get; }
        public int SampleRate { get; }

        public YinPitchDetector(int bufferSize, int sampleRate, float minHz = 70f, float maxHz = 1200f, float threshold = 0.15f)
        {
            if (bufferSize < 64) bufferSize = 64;
            if (sampleRate < 8000) sampleRate = 8000;
            if (minHz < 1f) minHz = 1f;
            if (maxHz <= minHz) maxHz = minHz + 1f;

            BufferSize = bufferSize;
            SampleRate = sampleRate;
            _threshold = threshold <= 0f ? 0.15f : threshold;

            int tauMin = (int)(sampleRate / maxHz);
            if (tauMin < 2) tauMin = 2;

            int tauMax = (int)Math.Ceiling(sampleRate / (double)minHz);
            int cap = bufferSize / 2;
            if (tauMax > cap) tauMax = cap;
            if (tauMax <= tauMin + 2) tauMax = tauMin + 3;
            if (tauMax > bufferSize - 2) tauMax = bufferSize - 2;

            _tauMin = tauMin;
            _tauMax = tauMax;

            _difference = new float[_tauMax + 1];
            _cmndf = new float[_tauMax + 1];
        }

        public float Detect(float[] buffer, int offset, int count, out float confidence)
        {
            confidence = 0f;
            if (buffer == null) return 0f;
            if (offset < 0 || count <= 0 || offset + count > buffer.Length) return 0f;

            int tauMax = _tauMax;
            if (tauMax > count / 2) tauMax = count / 2;
            if (tauMax <= _tauMin + 2) return 0f;

            // Fixed integration window across all tau, otherwise d(tau) values are not comparable.
            int window = count - tauMax;
            if (window < 2) return 0f;

            for (int tau = 1; tau <= tauMax; tau++)
            {
                // Double accumulator: d(tau) at the true period is a near-total cancellation,
                // and float rounding over ~1400 terms is large enough to move the CMNDF minimum.
                double sum = 0.0;
                int a = offset;
                int b = offset + tau;
                for (int j = 0; j < window; j++)
                {
                    double delta = buffer[a + j] - buffer[b + j];
                    sum += delta * delta;
                }
                _difference[tau] = (float)sum;
            }

            _cmndf[0] = 1f;
            double running = 0.0;
            for (int tau = 1; tau <= tauMax; tau++)
            {
                running += _difference[tau];
                _cmndf[tau] = running > 0.0 ? (float)(_difference[tau] * tau / running) : 1f;
            }

            int bestTau = -1;
            for (int tau = _tauMin; tau <= tauMax; tau++)
            {
                if (_cmndf[tau] >= _threshold) continue;
                while (tau + 1 <= tauMax && _cmndf[tau + 1] < _cmndf[tau]) tau++;
                bestTau = tau;
                break;
            }

            if (bestTau < 0)
            {
                float min = float.MaxValue;
                for (int tau = _tauMin; tau <= tauMax; tau++)
                {
                    if (_cmndf[tau] >= min) continue;
                    min = _cmndf[tau];
                    bestTau = tau;
                }
                if (bestTau < 0 || min > UnvoicedCeiling) return 0f;
            }

            float value = _cmndf[bestTau];
            confidence = 1f - value;
            if (confidence < 0f) confidence = 0f;
            else if (confidence > 1f) confidence = 1f;

            float refinedTau = Interpolate(bestTau, tauMax);
            if (refinedTau <= 0f)
            {
                confidence = 0f;
                return 0f;
            }

            return SampleRate / refinedTau;
        }

        private float Interpolate(int tau, int tauMax)
        {
            int left = tau > 1 ? tau - 1 : tau;
            int right = tau + 1 <= tauMax ? tau + 1 : tau;
            if (left == tau) return _cmndf[tau] <= _cmndf[right] ? tau : right;
            if (right == tau) return _cmndf[tau] <= _cmndf[left] ? tau : left;

            float s0 = _cmndf[left];
            float s1 = _cmndf[tau];
            float s2 = _cmndf[right];
            float denom = 2f * s1 - s2 - s0;
            if (denom == 0f) return tau;
            return tau + (s2 - s0) / (2f * denom);
        }

        public static float ComputeRms(float[] buffer, int offset, int count)
        {
            if (buffer == null || count <= 0) return 0f;
            if (offset < 0 || offset + count > buffer.Length) return 0f;

            double sum = 0.0;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                double v = buffer[i];
                sum += v * v;
            }
            return (float)Math.Sqrt(sum / count);
        }
    }
}
