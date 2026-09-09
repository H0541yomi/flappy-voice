using FlappyVoice.Audio;
using FlappyVoice.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    public sealed class HudUI : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PitchTracker pitchTracker;
        [SerializeField] private GameStateManager stateManager;

        [SerializeField] private CanvasGroup scoreGroup;
        [SerializeField] private TextMeshProUGUI scoreLabel;
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private TextMeshProUGUI micHintLabel;

        [SerializeField] private CanvasGroup singToStartGroup;
        [SerializeField] private RectTransform singToStartPulseTarget;
        [SerializeField] private float singToStartPulseHz = 0.6f;
        [SerializeField] private float singToStartMinAlpha = 0.62f;
        [SerializeField] private float singToStartScaleAmplitude = 0.045f;

        [SerializeField] private float fallbackGateRms = 0.015f;

        private static readonly string[] SmallScoreStrings = BuildSmallScoreStrings(128);

        private float gateRms = 0.015f;
        private int renderedScore = int.MinValue;
        private int renderedAudible = -1;
        private bool subscribed;
        private bool singToStartVisible;

        public void Configure(ScoreManager score, PitchTracker tracker, GameStateManager state)
        {
            Unsubscribe();
            scoreManager = score;
            pitchTracker = tracker;
            stateManager = state;
            RefreshGate();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            RenderScore(scoreManager != null ? scoreManager.Score : 0);
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        private void OnEnable()
        {
            RefreshGate();
            Subscribe();
            RenderScore(scoreManager != null ? scoreManager.Score : 0);
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }
            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged += OnScoreChanged;
            }
            if (stateManager != null)
            {
                stateManager.OnStateChanged += ApplyState;
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }
            if (scoreManager != null)
            {
                scoreManager.OnScoreChanged -= OnScoreChanged;
            }
            if (stateManager != null)
            {
                stateManager.OnStateChanged -= ApplyState;
            }
            subscribed = false;
        }

        private void RefreshGate()
        {
            gateRms = fallbackGateRms;
            if (stateManager != null && stateManager.Config != null)
            {
                gateRms = stateManager.Config.AmplitudeGateRms;
            }
        }

        private void Update()
        {
            float amplitude = pitchTracker != null ? pitchTracker.Current.Amplitude : 0f;

            int audible = amplitude >= gateRms ? 1 : 0;
            if (audible != renderedAudible)
            {
                renderedAudible = audible;
                if (micHintLabel != null)
                {
                    micHintLabel.enabled = audible == 0;
                }
            }

            PulseSingToStart();
        }

        private void PulseSingToStart()
        {
            if (!singToStartVisible || singToStartGroup == null)
            {
                return;
            }

            float phase = Mathf.Sin(Time.unscaledTime * singToStartPulseHz * (Mathf.PI * 2f));
            float t = (phase + 1f) * 0.5f;
            singToStartGroup.alpha = Mathf.Lerp(singToStartMinAlpha, 1f, t);
            if (singToStartPulseTarget != null)
            {
                float scale = 1f + (singToStartScaleAmplitude * phase);
                singToStartPulseTarget.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void OnScoreChanged(int value)
        {
            RenderScore(value);
        }

        private void RenderScore(int value)
        {
            if (value == renderedScore || scoreLabel == null)
            {
                return;
            }
            renderedScore = value;
            scoreLabel.SetText(value >= 0 && value < SmallScoreStrings.Length
                ? SmallScoreStrings[value]
                : value.ToString());
        }

        private void ApplyState(GameState state)
        {
            bool showScore = state == GameState.Playing;
            bool showHint = state != GameState.GameOver;
            if (scoreGroup != null)
            {
                scoreGroup.alpha = showScore ? 1f : 0f;
            }
            if (hintGroup != null)
            {
                hintGroup.alpha = showHint ? 1f : 0f;
            }

            singToStartVisible = state == GameState.Attract;
            if (singToStartGroup != null)
            {
                singToStartGroup.gameObject.SetActive(singToStartVisible);
                singToStartGroup.alpha = singToStartVisible ? 1f : 0f;
            }
            if (singToStartPulseTarget != null && !singToStartVisible)
            {
                singToStartPulseTarget.localScale = Vector3.one;
            }
        }

        private static string[] BuildSmallScoreStrings(int count)
        {
            string[] values = new string[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = i.ToString();
            }
            return values;
        }
    }
}
