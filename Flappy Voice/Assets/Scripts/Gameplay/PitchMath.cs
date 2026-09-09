using System;

namespace FlappyVoice.Gameplay
{
    public static class PitchMath
    {
        public const float A4Hz = 440f;
        public const int A4Midi = 69;

        private const double InvLog2 = 1.4426950408889634;

        // Interned literals indexed by pitch class (midi % 12, C = 0). Returned by reference so
        // per-pipe and per-frame labelling never allocates.
        private static readonly string[] ChromaticNames =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
        };

        public static float HzToMidi(float hz)
        {
            if (hz <= 0f) return 0f;
            return (float)(A4Midi + 12.0 * Math.Log(hz / (double)A4Hz) * InvLog2);
        }

        public static float MidiToHz(float midi)
        {
            return (float)(A4Hz * Math.Pow(2.0, (midi - A4Midi) / 12.0));
        }

        public static int RoundToSemitone(float midi)
        {
            return (int)Math.Round((double)midi, MidpointRounding.AwayFromZero);
        }

        // How far the playable range extends BELOW the note the player first sang. Integer division
        // keeps the floor on a whole semitone even for an odd range width, so every pipe offset
        // still lands on a real note that can be named and sung.
        public static int SemitonesBelowCenter(int rangeWidthSemitones)
        {
            return rangeWidthSemitones <= 0 ? 0 : rangeWidthSemitones / 2;
        }

        // The first sung note is rounded to the nearest semitone and placed at the MIDDLE of the
        // screen; the range then runs half a span down and half a span up from there.
        public static float CenteredFloorMidi(float centerMidi, int rangeWidthSemitones)
        {
            return RoundToSemitone(centerMidi) - SemitonesBelowCenter(rangeWidthSemitones);
        }

        public static float ClampToOctaveHeight(float midi, float floorMidi, int octaveWidthSemitones)
        {
            if (octaveWidthSemitones <= 0) return 0f;

            float height = (midi - floorMidi) / octaveWidthSemitones;
            if (height <= 0f) return 0f;
            if (height >= 1f) return 1f;
            return height;
        }

        public static string NoteNameForMidi(int midi)
        {
            int index = ((midi % 12) + 12) % 12;
            return ChromaticNames[index];
        }

        public static float HeightForOffset(int semitoneOffset, int octaveWidthSemitones)
        {
            if (octaveWidthSemitones <= 0) return 0f;

            float height = semitoneOffset / (float)octaveWidthSemitones;
            if (height <= 0f) return 0f;
            if (height >= 1f) return 1f;
            return height;
        }
    }
}
