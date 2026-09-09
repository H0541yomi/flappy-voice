using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using TMPro;
using UnityEngine;

namespace FlappyVoice.UI
{
    // A full-width horizontal line drawn at the height of the note currently being sung, plus that
    // note's letter. Built in world space, not on the canvas: the pipes and the bird live in world
    // units, and a canvas-space bar would only line up with them at one aspect ratio.
    public sealed class NoteBarUI : MonoBehaviour
    {
        [SerializeField] private VoiceHeightSource voiceSource;
        [SerializeField] private DevHeightSource devSource;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private GameConfig config;

        [SerializeField] private SpriteRenderer bar;
        [SerializeField] private TMP_Text noteLabel;
        [SerializeField] private float barThicknessUnits = 0.07f;
        [SerializeField] private float labelInsetUnits = 0.65f;
        [SerializeField] private Color inRangeColor = new Color(1f, 1f, 1f, 0.75f);
        [SerializeField] private Color clampedColor = new Color(1f, 0.72f, 0.28f, 0.9f);

        private int renderedMidi = int.MinValue;
        private int renderedClamped = -1;
        private bool visible = true;

        public void Configure(GameConfig gameConfig, VoiceHeightSource voice, DevHeightSource dev, Camera camera)
        {
            config = gameConfig;
            voiceSource = voice;
            devSource = dev;
            if (camera != null) viewCamera = camera;
            renderedMidi = int.MinValue;
            renderedClamped = -1;
        }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (config == null || voiceSource == null)
            {
                SetVisible(false);
                return;
            }

            if (!TryGetSungMidi(out int midi))
            {
                SetVisible(false);
                return;
            }

            int width = Mathf.Max(1, config.OctaveWidthSemitones);
            float height;
            bool clamped;

            if (voiceSource.IsAnchored)
            {
                float floor = voiceSource.FloorMidi;

                // Snapped to the semitone so the bar sits exactly where a pipe gap for that note
                // sits, which is what makes it usable for aiming rather than as a tuner read-out.
                height = PitchMath.ClampToOctaveHeight(midi, floor, width);

                // Out-of-range notes still show their REAL letter while the bar pins to the edge,
                // so a player singing above the ceiling can see that, rather than being told they
                // are on the top note.
                clamped = midi < floor || midi > floor + width;
            }
            else
            {
                // Nothing is anchored yet, and the note being sung right now is the one that will
                // become the middle of the range. Showing it at mid-screen is a preview of exactly
                // that, not an arbitrary placeholder position.
                height = 0.5f;
                clamped = false;
            }

            float y = Mathf.Lerp(config.PlayfieldMinY, config.PlayfieldMaxY, height);

            SetVisible(true);
            Layout(y, clamped);
            RenderLabel(midi);
        }

        private bool TryGetSungMidi(out int midi)
        {
            midi = 0;

            if (devSource != null && devSource.Enabled)
            {
                if (!voiceSource.IsAnchored)
                {
                    return false;
                }

                int width = Mathf.Max(1, config.OctaveWidthSemitones);
                int offset = Mathf.RoundToInt(Mathf.Clamp01(devSource.TargetHeight01) * width);
                midi = PitchMath.RoundToSemitone(voiceSource.FloorMidi) + offset;
                return true;
            }

            float current = voiceSource.CurrentMidi;
            if (current < 0f)
            {
                return false;
            }

            midi = PitchMath.RoundToSemitone(current);
            return true;
        }

        private void Layout(float y, bool clamped)
        {
            float halfWidth = viewCamera != null && viewCamera.orthographic
                ? viewCamera.orthographicSize * viewCamera.aspect
                : 5f;
            float centerX = viewCamera != null ? viewCamera.transform.position.x : 0f;

            if (bar != null)
            {
                bar.transform.position = new Vector3(centerX, y, 0.5f);
                bar.transform.localScale = new Vector3(halfWidth * 2f, Mathf.Max(0.01f, barThicknessUnits), 1f);
            }

            if (noteLabel != null)
            {
                noteLabel.transform.position =
                    new Vector3(centerX - halfWidth + labelInsetUnits, y + (barThicknessUnits * 0.5f) + 0.34f, 0.4f);
            }

            int clampedKey = clamped ? 1 : 0;
            if (clampedKey != renderedClamped)
            {
                renderedClamped = clampedKey;
                Color color = clamped ? clampedColor : inRangeColor;
                if (bar != null) bar.color = color;
                if (noteLabel != null) noteLabel.color = color;
            }
        }

        private void RenderLabel(int midi)
        {
            if (noteLabel == null || midi == renderedMidi)
            {
                return;
            }

            renderedMidi = midi;
            noteLabel.SetText(PitchMath.NoteNameForMidi(midi));
        }

        private void SetVisible(bool value)
        {
            if (value == visible)
            {
                return;
            }

            visible = value;
            if (bar != null) bar.enabled = value;
            if (noteLabel != null) noteLabel.enabled = value;
        }
    }
}
