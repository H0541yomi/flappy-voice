using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    // TODO: development aid, remove before shipping together with DevHeightSource. A toggle plus a
    // vertical slider down the left edge that flies the bird without a microphone.
    public sealed class DevPanelUI : MonoBehaviour
    {
        [SerializeField] private DevHeightSource devSource;
        [SerializeField] private VoiceHeightSource voiceSource;
        [SerializeField] private GameConfig config;

        [SerializeField] private Toggle enableToggle;
        [SerializeField] private Slider heightSlider;
        [SerializeField] private CanvasGroup sliderGroup;
        [SerializeField] private TextMeshProUGUI readout;

        // Dev mode never sings, so nothing would anchor the range and every note letter would stay
        // "?". A4 is an arbitrary but singable centre that makes the letters real.
        [SerializeField] private float fallbackAnchorMidi = 69f;

        private bool listening;
        private int renderedMidi = int.MinValue;

        public void Configure(GameConfig gameConfig, DevHeightSource dev, VoiceHeightSource voice)
        {
            if (voiceSource != null)
            {
                voiceSource.OnAnchorChanged -= HandleAnchorChanged;
            }

            config = gameConfig;
            devSource = dev;
            voiceSource = voice;

            if (voiceSource != null)
            {
                voiceSource.OnAnchorChanged += HandleAnchorChanged;
            }

            ApplyEnabled(devSource != null && devSource.Enabled);
        }

        private void OnDestroy()
        {
            if (voiceSource != null)
            {
                voiceSource.OnAnchorChanged -= HandleAnchorChanged;
            }
        }

        // Play Again resets the run, which clears the anchor. Nothing re-anchors it while dev mode
        // is driving, so without this the pipe letters and the note bar go dead after one death.
        private void HandleAnchorChanged()
        {
            if (devSource == null || !devSource.Enabled || voiceSource == null || voiceSource.IsAnchored)
            {
                return;
            }

            voiceSource.ForceAnchor(fallbackAnchorMidi);
        }

        private void OnEnable()
        {
            if (listening)
            {
                return;
            }
            listening = true;

            if (enableToggle != null)
            {
                enableToggle.onValueChanged.AddListener(HandleToggle);
                enableToggle.SetIsOnWithoutNotify(devSource != null && devSource.Enabled);
            }
            if (heightSlider != null)
            {
                heightSlider.onValueChanged.AddListener(HandleSlider);
            }

            ApplyEnabled(devSource != null && devSource.Enabled);
        }

        private void OnDisable()
        {
            if (!listening)
            {
                return;
            }
            listening = false;

            if (enableToggle != null) enableToggle.onValueChanged.RemoveListener(HandleToggle);
            if (heightSlider != null) heightSlider.onValueChanged.RemoveListener(HandleSlider);
        }

        private void HandleToggle(bool on)
        {
            if (on && voiceSource != null && !voiceSource.IsAnchored)
            {
                voiceSource.ForceAnchor(fallbackAnchorMidi);
            }

            ApplyEnabled(on);

            if (on && devSource != null && heightSlider != null)
            {
                devSource.SetHeight01(heightSlider.value);
            }
        }

        private void HandleSlider(float value)
        {
            if (devSource != null)
            {
                devSource.SetHeight01(value);
            }
            RenderReadout(value);
        }

        private void ApplyEnabled(bool on)
        {
            if (devSource != null)
            {
                devSource.SetEnabled(on);
            }
            if (sliderGroup != null)
            {
                sliderGroup.alpha = on ? 1f : 0.35f;
                sliderGroup.interactable = on;
                sliderGroup.blocksRaycasts = on;
            }
            RenderReadout(heightSlider != null ? heightSlider.value : 0.5f);
        }

        private void RenderReadout(float value)
        {
            if (readout == null)
            {
                return;
            }

            int midi;
            if (config != null && voiceSource != null && voiceSource.IsAnchored)
            {
                int width = Mathf.Max(1, config.OctaveWidthSemitones);
                midi = PitchMath.RoundToSemitone(voiceSource.FloorMidi) + Mathf.RoundToInt(Mathf.Clamp01(value) * width);
            }
            else
            {
                midi = int.MinValue;
            }

            if (midi == renderedMidi)
            {
                return;
            }
            renderedMidi = midi;
            readout.SetText(midi == int.MinValue ? "DEV" : PitchMath.NoteNameForMidi(midi));
        }
    }
}
