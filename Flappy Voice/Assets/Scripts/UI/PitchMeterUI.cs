using FlappyVoice.Audio;
using FlappyVoice.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    public sealed class PitchMeterUI : MonoBehaviour
    {
        [SerializeField] private VoiceHeightSource voiceSource;
        [SerializeField] private PitchTracker pitchTracker;

        [SerializeField] private RectTransform indicator;
        [SerializeField] private Image indicatorGraphic;
        [SerializeField] private Image wrapFlashTop;
        [SerializeField] private Image wrapFlashBottom;
        [SerializeField] private CanvasGroup bandGroup;
        [SerializeField] private TextMeshProUGUI noteLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;

        [SerializeField] private float wrapFlashSec = 0.45f;
        [SerializeField] private float wrapDetectMargin = 0.3f;
        [SerializeField] private float unvoicedAlpha = 0.25f;

        private static readonly string[] NoteNames =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
        };

        private const string ListeningText = "sing to set your low note";
        private const string WrapText = "top of octave wraps to bottom";

        private float previousHeight = -1f;
        private float flashTop;
        private float flashBottom;
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
            previousHeight = -1f;
            renderedNoteKey = int.MinValue;
            renderedStatus = -1;
        }

        private void OnEnable()
        {
            previousHeight = -1f;
            flashTop = 0f;
            flashBottom = 0f;
            renderedNoteKey = int.MinValue;
            renderedStatus = -1;
            renderedVoiced = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool anchored = voiceSource != null && voiceSource.IsAnchored;
            bool voiced = pitchTracker != null && pitchTracker.HasVoice;
            float height = voiceSource != null ? Mathf.Clamp01(voiceSource.TargetHeight01) : 0.5f;

            if (previousHeight >= 0f && voiced && anchored)
            {
                if (previousHeight > 1f - wrapDetectMargin && height < wrapDetectMargin)
                {
                    flashBottom = 1f;
                }
                else if (previousHeight < wrapDetectMargin && height > 1f - wrapDetectMargin)
                {
                    flashTop = 1f;
                }
            }
            previousHeight = height;

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

            float decay = wrapFlashSec > Mathf.Epsilon ? dt / wrapFlashSec : 1f;
            flashTop = Mathf.Max(0f, flashTop - decay);
            flashBottom = Mathf.Max(0f, flashBottom - decay);
            ApplyFlash(wrapFlashTop, flashTop);
            ApplyFlash(wrapFlashBottom, flashBottom);

            RenderNote(voiced ? pitchTracker.Current.FrequencyHz : 0f);
            RenderStatus(anchored, flashTop > 0f || flashBottom > 0f);
        }

        private static void ApplyFlash(Image target, float amount)
        {
            if (target == null)
            {
                return;
            }
            Color c = target.color;
            c.a = amount;
            target.color = c;
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

        private void RenderStatus(bool anchored, bool wrapping)
        {
            if (statusLabel == null)
            {
                return;
            }
            int status = !anchored ? 0 : wrapping ? 1 : 2;
            if (status == renderedStatus)
            {
                return;
            }
            renderedStatus = status;
            switch (status)
            {
                case 0:
                    statusLabel.SetText(ListeningText);
                    break;
                case 1:
                    statusLabel.SetText(WrapText);
                    break;
                default:
                    statusLabel.SetText(string.Empty);
                    break;
            }
        }
    }
}
