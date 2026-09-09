using FlappyVoice.Audio;
using FlappyVoice.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    // TODO: temporarily disabled, not dead code. SceneBuilder leaves the scene object present but
    // inactive ("PitchMeter (DISABLED TODO)") because the 12-segment bar was drawn for the old
    // wrapping octave; it needs a redesign for the clamped A-to-A range (offset 0 = bottom,
    // offset 12 = top, out-of-range pins instead of wrapping) before it goes back into play.
    // The wrap seam it used to flash no longer exists, so that affordance is gone from here too.
    public sealed class PitchMeterUI : MonoBehaviour
    {
        [SerializeField] private VoiceHeightSource voiceSource;
        [SerializeField] private PitchTracker pitchTracker;

        [SerializeField] private RectTransform indicator;
        [SerializeField] private Image indicatorGraphic;
        [SerializeField] private CanvasGroup bandGroup;
        [SerializeField] private TextMeshProUGUI noteLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;

        [SerializeField] private float unvoicedAlpha = 0.25f;

        private static readonly string[] NoteNames =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
        };

        private const string ListeningText = "sing to set your low note";

        private int renderedNoteKey = int.MinValue;
        private int renderedStatus = -1;
        private bool renderedVoiced = true;

        public static Color ColorForHeight(float height01)
        {
            float hue = Mathf.Repeat(height01, 1f) * 0.85f;
            return Color.HSVToRGB(hue, 0.72f, 1f);
        }

        public void Configure(VoiceHeightSource voice, PitchTracker tracker)
        {
            voiceSource = voice;
            pitchTracker = tracker;
            renderedNoteKey = int.MinValue;
            renderedStatus = -1;
        }

        private void OnEnable()
        {
            renderedNoteKey = int.MinValue;
            renderedStatus = -1;
            renderedVoiced = true;
        }

        private void Update()
        {
            bool anchored = voiceSource != null && voiceSource.IsAnchored;
            bool voiced = pitchTracker != null && pitchTracker.HasVoice;
            float height = voiceSource != null ? Mathf.Clamp01(voiceSource.TargetHeight01) : 0.5f;

            if (indicator != null)
            {
                indicator.anchorMin = new Vector2(0f, height);
                indicator.anchorMax = new Vector2(1f, height);
                indicator.anchoredPosition = Vector2.zero;
            }
            if (indicatorGraphic != null)
            {
                indicatorGraphic.color = ColorForHeight(height);
            }

            if (voiced != renderedVoiced)
            {
                renderedVoiced = voiced;
                if (bandGroup != null)
                {
                    bandGroup.alpha = voiced ? 1f : unvoicedAlpha;
                }
            }

            RenderNote(voiced ? pitchTracker.Current.FrequencyHz : 0f);
            RenderStatus(anchored);
        }

        private void RenderNote(float hz)
        {
            if (noteLabel == null)
            {
                return;
            }
            if (hz <= 0f)
            {
                if (renderedNoteKey != int.MinValue)
                {
                    renderedNoteKey = int.MinValue;
                    noteLabel.SetText("--");
                }
                return;
            }

            int midi = Mathf.RoundToInt(PitchMath.HzToMidi(hz));
            if (midi == renderedNoteKey)
            {
                return;
            }
            renderedNoteKey = midi;
            int index = ((midi % 12) + 12) % 12;
            noteLabel.SetText($"{NoteNames[index]}{(midi / 12) - 1}");
        }

        private void RenderStatus(bool anchored)
        {
            if (statusLabel == null)
            {
                return;
            }
            int status = anchored ? 1 : 0;
            if (status == renderedStatus)
            {
                return;
            }
            renderedStatus = status;
            statusLabel.SetText(anchored ? string.Empty : ListeningText);
        }
    }
}
