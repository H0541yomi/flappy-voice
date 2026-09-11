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

        // How far the range extends below a note that is to sit at `height01`. Rounded to a whole
        // semitone, and clamped so the note itself always stays inside the range it defines.
        public static int SemitonesBelowHeight(float height01, int rangeWidthSemitones)
        {
            if (rangeWidthSemitones <= 0) return 0;

            float clamped = height01 <= 0f ? 0f : (height01 >= 1f ? 1f : height01);
            int offset = RoundToSemitone(clamped * rangeWidthSemitones);
            return offset < 0 ? 0 : (offset > rangeWidthSemitones ? rangeWidthSemitones : offset);
        }

        // Anchors the range so that `noteMidi` lands at `height01` on screen rather than always in
        // the middle: the first note the player sings is aimed at the pipe they are about to meet.
        public static float FloorMidiForNoteAtHeight(float noteMidi, float height01, int rangeWidthSemitones)
        {
            return RoundToSemitone(noteMidi) - SemitonesBelowHeight(height01, rangeWidthSemitones);
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

        // Signed distance in cents from the nearest semitone, in -50..+50. Drives the tuner bar,
        // which reads the deviation rather than the note.
        public static float CentsFromNearestSemitone(float midi)
        {
            return (midi - RoundToSemitone(midi)) * 100f;
        }

        // A pipe gap spans the two adjacent notes `lowerOffset` and `lowerOffset + 1`, so its
        // centre sits on the boundary BETWEEN them, not on either note. That is what makes both
        // notes pass through one gap with the same margin.
        public static float HeightForNotePair(int lowerOffset, int octaveWidthSemitones)
        {
            if (octaveWidthSemitones <= 0) return 0f;

            float height = (lowerOffset + 0.5f) / octaveWidthSemitones;
            if (height <= 0f) return 0f;
            if (height >= 1f) return 1f;
            return height;
        }

        // The band of pitches that clears a pipe gap, in MIDI. The bird's centre has to stay inside
        // the opening by its own radius, and the pitch-to-height mapping turns that band of screen
        // positions back into a band of notes: the edges are the pitches that just barely miss the
        // walls. False when the opening is narrower than the bird - nothing clears it.
        public static bool TrySafePitchWindow(float gapCenterY, float gapSize, float bodyRadiusUnits,
            float playfieldMinY, float playfieldMaxY, float floorMidi, int rangeWidthSemitones,
            out float lowMidi, out float highMidi)
        {
            lowMidi = 0f;
            highMidi = 0f;

            float span = playfieldMaxY - playfieldMinY;
            if (span <= 0f || rangeWidthSemitones <= 0) return false;

            float reach = (gapSize * 0.5f) - bodyRadiusUnits;
            if (reach <= 0f) return false;

            float semitonesPerUnit = rangeWidthSemitones / span;
            lowMidi = floorMidi + ((gapCenterY - reach) - playfieldMinY) * semitonesPerUnit;
            highMidi = floorMidi + ((gapCenterY + reach) - playfieldMinY) * semitonesPerUnit;

            // Pitch above the ceiling parks the bird ON the ceiling, so if the ceiling itself
            // clears the gap then so does every note above it, however far off it is. Same at the
            // floor. Reporting the raw band would call those notes unsafe when they are not.
            float ceilingMidi = floorMidi + rangeWidthSemitones;
            if (highMidi >= ceilingMidi) highMidi = ceilingMidi + rangeWidthSemitones;
            if (lowMidi <= floorMidi) lowMidi = floorMidi - rangeWidthSemitones;

            return true;
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
