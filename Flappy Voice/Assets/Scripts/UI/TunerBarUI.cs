using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    // Chromatic tuner strip across the top of the screen: a sliding dial of note letters over a
    // ten-cent tick ruler, read against a fixed needle in the middle. The dial - letters and ruler
    // together - is one rigid strip that only ever moves horizontally, so the needle's distance
    // from a letter IS the sung deviation from that note, down to a tick.
    //
    // The green band is the band of pitches that clears the gap ahead, worked out from that gap's
    // real size and the bird's real collider rather than from a fixed tolerance: its edges are the
    // pitches that just barely miss the pipe. Needle inside the band means the note being held
    // will get through.
    //
    // Canvas space rather than world space: this reads pitch, it does not have to agree with where
    // the pipes are, so it belongs to the HUD and should keep its size on every aspect ratio.
    public sealed class TunerBarUI : MonoBehaviour
    {
        // Note letters sit one semitone apart on the dial; the ruler SceneBuilder draws under them
        // uses the same figure. Changing it here alone would put the letters out of step with the
        // ticks they are read against.
        public const float PixelsPerSemitone = 216f;

        // Odd, so one label is the centre one that the needle points at when the note is in tune.
        public const int NoteSlotCount = 7;

        [SerializeField] private VoiceHeightSource voiceSource;
        [SerializeField] private GameConfig config;
        [SerializeField] private PipeSpawner pipeSpawner;
        [SerializeField] private PlayerController player;

        [SerializeField] private RectTransform dial;
        [SerializeField] private CanvasGroup dialGroup;
        [SerializeField] private TMP_Text[] noteLabels = new TMP_Text[NoteSlotCount];
        [SerializeField] private RectTransform safeBand;
        [SerializeField] private Graphic needle;
        [SerializeField] private TMP_Text noteReadout;
        [SerializeField] private TMP_Text centsReadout;

        [SerializeField] private float pixelsPerSemitone = PixelsPerSemitone;
        [SerializeField] private float silentAlpha = 0.3f;
        [SerializeField] private Color safeColor = new Color(0.36f, 0.85f, 0.51f, 1f);
        [SerializeField] private Color offTuneColor = new Color(0.93f, 0.27f, 0.31f, 1f);
        [SerializeField] private Color inRangeLabelColor = Color.white;
        [SerializeField] private Color outOfRangeLabelColor = new Color(1f, 0.72f, 0.28f, 1f);

        // The bird is still inside a gap for a moment after its centre has passed the pipe, so the
        // query starts slightly behind the bird: the band must keep describing the gap being flown
        // through, not the next one along.
        private const float GapLookBehindUnits = 1.2f;
        private const float FallbackBodyRadiusUnits = 0.42f;

        private const string SilentNoteText = "--";
        private const string SilentCentsText = "";

        // Cents are only ever reported in -50..+50, so every string the readout can need is
        // interned up front rather than formatted on a frame where the note drifts.
        private static readonly string[] CentsStrings = BuildCentsStrings();

        private int renderedNearestMidi = int.MinValue;
        private int renderedNoteReadoutMidi = int.MinValue;
        private int renderedCents = int.MinValue;
        private int renderedOutOfRange = -1;
        private int renderedSafe = -1;
        private bool renderedSafeBandVisible = true;
        private bool renderedVoiced = true;

        public void Configure(GameConfig gameConfig, VoiceHeightSource voice, PipeSpawner spawner,
            PlayerController playerController)
        {
            config = gameConfig;
            voiceSource = voice;
            pipeSpawner = spawner;
            player = playerController;
            InvalidateRendered();
        }

        private void OnEnable()
        {
            InvalidateRendered();
        }

        private void Update()
        {
            float midi = voiceSource != null ? voiceSource.CurrentMidi : -1f;
            bool voiced = midi >= 0f;

            SetVoiced(voiced);
            if (!voiced)
            {
                // The dial is left exactly where the last sung note put it: snapping it back to a
                // default note would read as the player having sung that note.
                return;
            }

            int nearest = PitchMath.RoundToSemitone(midi);
            float cents = PitchMath.CentsFromNearestSemitone(midi);

            if (dial != null)
            {
                Vector2 position = dial.anchoredPosition;
                position.x = -(midi - nearest) * Mathf.Max(1f, pixelsPerSemitone);
                dial.anchoredPosition = position;
            }

            // Whether the note steers the bird changes the colour of every letter, so a change of
            // range state has to invalidate the labels the same way a change of note does.
            int outOfRangeKey = InPlayableRange(nearest) ? 0 : 1;
            if (outOfRangeKey != renderedOutOfRange)
            {
                renderedOutOfRange = outOfRangeKey;
                renderedNearestMidi = int.MinValue;
                renderedNoteReadoutMidi = int.MinValue;
            }

            RenderLabels(nearest);
            RenderSafeBand(midi);
            RenderReadouts(nearest, cents);
        }

        private void RenderLabels(int nearestMidi)
        {
            if (nearestMidi == renderedNearestMidi || noteLabels == null)
            {
                return;
            }

            renderedNearestMidi = nearestMidi;
            int center = noteLabels.Length / 2;

            for (int i = 0; i < noteLabels.Length; i++)
            {
                TMP_Text label = noteLabels[i];
                if (label == null)
                {
                    continue;
                }

                int midi = nearestMidi + (i - center);
                label.SetText(PitchMath.NoteNameForMidi(midi));
                label.color = InPlayableRange(midi) ? inRangeLabelColor : outOfRangeLabelColor;
            }
        }

        // Out of the anchored range the letter is still the truth about what is being sung, but the
        // bird is pinned to the floor or the ceiling, so the tuner has to say that the note is no
        // longer steering anything.
        private bool InPlayableRange(int midi)
        {
            if (config == null || voiceSource == null || !voiceSource.IsAnchored)
            {
                return true;
            }

            int floor = PitchMath.RoundToSemitone(voiceSource.FloorMidi);
            return midi >= floor && midi <= floor + Mathf.Max(1, config.OctaveWidthSemitones);
        }

        // Positioned against the needle rather than on the sliding dial: the band is a statement
        // about pitch, and the pitch it is compared against is whatever the needle is pointing at.
        private void RenderSafeBand(float midi)
        {
            if (!TrySafeWindow(out float lowMidi, out float highMidi))
            {
                SetSafeBandVisible(false);
                RenderNeedleSafety(false);
                return;
            }

            float centerMidi = (lowMidi + highMidi) * 0.5f;
            float px = Mathf.Max(1f, pixelsPerSemitone);

            SetSafeBandVisible(true);
            if (safeBand != null)
            {
                safeBand.anchoredPosition = new Vector2((centerMidi - midi) * px, safeBand.anchoredPosition.y);
                safeBand.sizeDelta = new Vector2((highMidi - lowMidi) * px, safeBand.sizeDelta.y);
            }

            RenderNeedleSafety(midi >= lowMidi && midi <= highMidi);
        }

        private bool TrySafeWindow(out float lowMidi, out float highMidi)
        {
            lowMidi = 0f;
            highMidi = 0f;

            if (config == null || voiceSource == null || pipeSpawner == null || !voiceSource.IsAnchored)
            {
                return false;
            }

            float birdX = player != null ? player.transform.position.x : 0f;
            if (!pipeSpawner.TryGetNextGapAhead(birdX - GapLookBehindUnits, out float gapCenterY, out float gapSize))
            {
                return false;
            }

            return PitchMath.TrySafePitchWindow(gapCenterY, gapSize,
                player != null ? player.BodyRadiusUnits : FallbackBodyRadiusUnits,
                config.PlayfieldMinY, config.PlayfieldMaxY, voiceSource.FloorMidi,
                Mathf.Max(1, config.OctaveWidthSemitones), out lowMidi, out highMidi);
        }

        private void RenderNeedleSafety(bool safe)
        {
            int key = safe ? 1 : 0;
            if (key == renderedSafe || needle == null)
            {
                return;
            }

            renderedSafe = key;
            needle.color = safe ? safeColor : offTuneColor;
        }

        private void SetSafeBandVisible(bool value)
        {
            if (safeBand == null || value == renderedSafeBandVisible)
            {
                return;
            }

            renderedSafeBandVisible = value;
            safeBand.gameObject.SetActive(value);
        }

        private void RenderReadouts(int nearestMidi, float cents)
        {
            if (noteReadout != null && nearestMidi != renderedNoteReadoutMidi)
            {
                renderedNoteReadoutMidi = nearestMidi;
                // Concatenated only when the note actually changes, which is orders of magnitude
                // rarer than a frame.
                noteReadout.SetText(PitchMath.NoteNameForMidi(nearestMidi) + OctaveNumber(nearestMidi));
                noteReadout.color = renderedOutOfRange == 1 ? outOfRangeLabelColor : inRangeLabelColor;
            }

            int rounded = Mathf.Clamp(Mathf.RoundToInt(cents), -50, 50);
            if (centsReadout != null && rounded != renderedCents)
            {
                renderedCents = rounded;
                centsReadout.SetText(CentsStrings[rounded + 50]);
            }
        }

        // Scientific pitch notation: MIDI 60 is C4.
        private static int OctaveNumber(int midi)
        {
            return Mathf.FloorToInt(midi / 12f) - 1;
        }

        private void SetVoiced(bool voiced)
        {
            if (voiced == renderedVoiced)
            {
                return;
            }

            renderedVoiced = voiced;

            if (dialGroup != null)
            {
                dialGroup.alpha = voiced ? 1f : Mathf.Clamp01(silentAlpha);
            }

            if (voiced)
            {
                return;
            }

            renderedCents = int.MinValue;
            renderedSafe = -1;
            SetSafeBandVisible(false);
            if (needle != null) needle.color = offTuneColor;
            if (noteReadout != null) noteReadout.SetText(SilentNoteText);
            if (centsReadout != null) centsReadout.SetText(SilentCentsText);
            renderedNoteReadoutMidi = int.MinValue;
        }

        private void InvalidateRendered()
        {
            renderedNearestMidi = int.MinValue;
            renderedNoteReadoutMidi = int.MinValue;
            renderedCents = int.MinValue;
            renderedOutOfRange = -1;
            renderedSafe = -1;
            renderedSafeBandVisible = true;
            renderedVoiced = true;
        }

        private static string[] BuildCentsStrings()
        {
            string[] values = new string[101];
            for (int i = 0; i < values.Length; i++)
            {
                int cents = i - 50;
                values[i] = cents > 0 ? "+" + cents.ToString() + "¢" : cents.ToString() + "¢";
            }
            return values;
        }
    }
}
