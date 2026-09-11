using FlappyVoice.Gameplay;
using TMPro;
using UnityEngine;

namespace FlappyVoice.UI
{
    public sealed class HudUI : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private GameStateManager stateManager;

        [SerializeField] private CanvasGroup scoreGroup;
        [SerializeField] private TextMeshProUGUI scoreLabel;

        [SerializeField] private CanvasGroup singToStartGroup;
        [SerializeField] private RectTransform singToStartPulseTarget;
        [SerializeField] private float singToStartPulseHz = 0.6f;
        [SerializeField] private float singToStartMinAlpha = 0.62f;
        [SerializeField] private float singToStartScaleAmplitude = 0.045f;

        private static readonly string[] SmallScoreStrings = BuildSmallScoreStrings(128);

        private int renderedScore = int.MinValue;
        private bool subscribed;
        private bool singToStartVisible;

        public void Configure(ScoreManager score, GameStateManager state)
        {
            Unsubscribe();
            scoreManager = score;
            stateManager = state;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            RenderScore(scoreManager != null ? scoreManager.Score : 0);
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        private void OnEnable()
        {
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

        private void Update()
        {
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
            if (scoreGroup != null)
            {
                scoreGroup.alpha = state == GameState.Playing ? 1f : 0f;
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
