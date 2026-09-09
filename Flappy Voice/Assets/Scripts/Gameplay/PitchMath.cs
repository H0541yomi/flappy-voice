using System;

namespace FlappyVoice.Gameplay
{
    public static class PitchMath
    {
        public const float A4Hz = 440f;
        public const int A4Midi = 69;
        public const int APitchClass = 9;

        private const double InvLog2 = 1.4426950408889634;

        // Interned literals, indexed by semitone offset above the A floor. Returned by reference so
        // per-pipe labelling on the hot path never allocates.
        private static readonly string[] OffsetNoteNames =
        {
            "A", "A#", "B", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A"
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

        // Highest midi note with pitch class A (midi % 12 == 9) that is <= midi. Every pipe's note
        // letter is derived from the floor, so the floor may only ever be an A.
        public static float NearestAFloorAtOrBelow(float midi)
        {
            double rel = (double)midi - APitchClass;
            double octaves = Math.Floor(rel / 12.0);
            return (float)(octaves * 12.0 + APitchClass);
        }

        public static float ClampToOctaveHeight(float midi, float floorMidi, int octaveWidthSemitones)
        {
            if (octaveWidthSemitones <= 0) return 0f;

            float height = (midi - floorMidi) / octaveWidthSemitones;
            if (height <= 0f) return 0f;
            if (height >= 1f) return 1f;
            return height;
        }

        public static string NoteNameForOffset(int semitoneOffset)
        {
            if (semitoneOffset < 0) semitoneOffset = 0;
            else if (semitoneOffset >= OffsetNoteNames.Length) semitoneOffset = OffsetNoteNames.Length - 1;
            return OffsetNoteNames[semitoneOffset];
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
