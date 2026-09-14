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
        [SerializeField] private TextMeshProUGUI singToStartHint;
        [SerializeField] private RectTransform singToStartPulseTarget;
        [SerializeField] private float singToStartPulseHz = 0.6f;
        // Gentle: the pulse target is now a full-height sign rather than a small plate, so the
        // old 4.5% breathed it a good 60 px and the parchment read as wobbling.
        [SerializeField] private float singToStartMinAlpha = 0.86f;
        [SerializeField] private float singToStartScaleAmplitude = 0.013f;

        private static readonly string[] SmallScoreStrings = BuildSmallScoreStrings(128);

        private int renderedScore = int.MinValue;
        private bool subscribed;
        private bool singToStartVisible;

        // The hint's resting copy lives in SceneBuilder, not here; it is captured on the first
        // override so restoring it never has to duplicate the authored string.
        private string authoredStartHint;
        private string renderedStartHint;

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

        // Null restores the sign's authored hint. Called while the microphone is still being
        // negotiated, which on the web can take until the player taps.
        public void SetStartHint(string hint)
        {
            if (singToStartHint == null) return;

            if (authoredStartHint == null)
            {
                authoredStartHint = singToStartHint.text;
                renderedStartHint = authoredStartHint;
            }

            string next = hint ?? authoredStartHint;
            if (next == renderedStartHint) return;

            renderedStartHint = next;
            singToStartHint.SetText(next);
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
