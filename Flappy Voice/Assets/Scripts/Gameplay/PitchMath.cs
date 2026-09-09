using System;

namespace FlappyVoice.Gameplay
{
    public static class PitchMath
    {
        public const float A4Hz = 440f;
        public const int A4Midi = 69;

        private const double InvLog2 = 1.4426950408889634;

        public static float HzToMidi(float hz)
        {
            if (hz <= 0f) return 0f;
            return (float)(A4Midi + 12.0 * Math.Log(hz / (double)A4Hz) * InvLog2);
        }

        public static float MidiToHz(float midi)
        {
            return (float)(A4Hz * Math.Pow(2.0, (midi - A4Midi) / 12.0));
        }

        public static float WrapToOctaveHeight(float midi, float floorMidi, int octaveWidthSemitones)
        {
            if (octaveWidthSemitones <= 0) return 0f;

            float width = octaveWidthSemitones;
            // C# '%' keeps the sign of the dividend; singing below the anchored floor is normal
            // and must wrap to the TOP of the playfield, never produce a negative height.
            float rel = (midi - floorMidi) % width;
            if (rel < 0f) rel += width;

            float height = rel / width;
            if (height < 0f || height >= 1f) return 0f;
            return height;
        }
    }
}
