namespace FlappyVoice.Audio
{
    public struct PitchSample
    {
        public long TimestampMs;
        public float FrequencyHz;
        public float Amplitude;
        public float Confidence;
        public bool IsVoiced;
    }
}
