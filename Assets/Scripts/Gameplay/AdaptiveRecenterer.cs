using System;

namespace FlappyVoice.Gameplay
{
    public sealed class AdaptiveRecenterer
    {
        private const float TwoPi = 6.2831853071795862f;
        private const int MinCapacity = 8;
        private const int MaxCapacity = 4096;
        private const float AssumedMaxFps = 240f;

        private float[] _sin;
        private float[] _cos;
        private float[] _dt;

        private int _head;
        private int _count;
        private float _sinSum;
        private float _cosSum;
        private float _windowedSec;

        private float _windowSec = 4f;
        private float _driftRatePerSec = 0.35f;
        private float _edgeThreshold = 0.15f;
        private int _octaveWidthSemitones = 12;

        public float CircularMeanHeight { get; private set; }
        public bool IsHuggingEdge { get; private set; }

        public AdaptiveRecenterer()
        {
            Configure(_windowSec, _driftRatePerSec, _edgeThreshold, _octaveWidthSemitones);
        }

        public void Configure(float windowSec, float driftRatePerSec, float edgeThreshold, int octaveWidthSemitones = 12)
        {
            _windowSec = windowSec > 0f ? windowSec : 0.001f;
            _driftRatePerSec = driftRatePerSec > 0f ? driftRatePerSec : 0f;
            _edgeThreshold = edgeThreshold;
            _octaveWidthSemitones = octaveWidthSemitones > 0 ? octaveWidthSemitones : 12;

            int capacity = (int)Math.Ceiling(_windowSec * AssumedMaxFps);
            if (capacity < MinCapacity) capacity = MinCapacity;
            else if (capacity > MaxCapacity) capacity = MaxCapacity;

            if (_sin == null || _sin.Length != capacity)
            {
                _sin = new float[capacity];
                _cos = new float[capacity];
                _dt = new float[capacity];
            }

            Reset();
        }

        public void Reset()
        {
            _head = 0;
            _count = 0;
            _sinSum = 0f;
            _cosSum = 0f;
            _windowedSec = 0f;
            CircularMeanHeight = 0.5f;
            IsHuggingEdge = false;
        }

        public float Update(float normalizedHeight, float currentFloorMidi, float deltaTime)
        {
            if (deltaTime < 0f) deltaTime = 0f;

            Push(normalizedHeight, deltaTime);

            // Height 0 and height 1 are the SAME point on the octave wrap, so an arithmetic
            // mean of {0.98, 0.02} would report 0.5 (dead centre) for a player sat on the seam.
            float mean = (float)(Math.Atan2(_sinSum, _cosSum) / TwoPi);
            mean -= (float)Math.Floor(mean);
            if (mean < 0f || mean >= 1f) mean = 0f;
            CircularMeanHeight = mean;

            float distanceToSeam = mean < 1f - mean ? mean : 1f - mean;
            IsHuggingEdge = distanceToSeam < _edgeThreshold;

            if (!IsHuggingEdge) return currentFloorMidi;

            // height = ((midi - floor) mod width) / width, so raising the floor LOWERS the height.
            float idealDeltaSemitones = (mean - 0.5f) * _octaveWidthSemitones;
            float maxStep = _driftRatePerSec * deltaTime;
            if (maxStep <= 0f) return currentFloorMidi;

            float step = idealDeltaSemitones;
            if (step > maxStep) step = maxStep;
            else if (step < -maxStep) step = -maxStep;

            return currentFloorMidi + step;
        }

        private void Push(float normalizedHeight, float deltaTime)
        {
            int capacity = _sin.Length;

            float angle = TwoPi * normalizedHeight;
            float s = (float)Math.Sin(angle);
            float c = (float)Math.Cos(angle);

            if (_count == capacity) PopOldest();

            int index = (_head + _count) % capacity;
            _sin[index] = s;
            _cos[index] = c;
            _dt[index] = deltaTime;
            _count++;
            _sinSum += s;
            _cosSum += c;
            _windowedSec += deltaTime;

            while (_count > 1 && _windowedSec - _dt[_head] >= _windowSec) PopOldest();
        }

        private void PopOldest()
        {
            _sinSum -= _sin[_head];
            _cosSum -= _cos[_head];
            _windowedSec -= _dt[_head];
            _head = (_head + 1) % _sin.Length;
            _count--;
        }
    }
}
