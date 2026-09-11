using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class ScoreManager : MonoBehaviour
    {
        public const string BestScoreKey = "flappyvoice.bestscore";

        private GameStateManager _state;
        private bool _committed;

        public int Score { get; private set; }
        public int BestScore { get; private set; }
        public bool IsNewBest { get; private set; }

        public event System.Action<int> OnScoreChanged;

        private void Awake()
        {
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        }

        public void Configure(GameStateManager state)
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }

            _state = state;

            if (_state != null)
            {
                _state.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnStateChanged -= HandleStateChanged;
            }
        }

        public void AddPoint()
        {
            Score++;
            OnScoreChanged?.Invoke(Score);
        }

        public void ResetScore()
        {
            Score = 0;
            IsNewBest = false;
            _committed = false;
            OnScoreChanged?.Invoke(Score);
        }

        public void CommitBestScore()
        {
            if (_committed)
            {
                return;
            }

            _committed = true;

            if (Score > BestScore)
            {
                BestScore = Score;
                IsNewBest = true;
            }

            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            PlayerPrefs.Save();
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    ResetScore();
                    break;
                case GameState.GameOver:
                    CommitBestScore();
                    break;
                case GameState.Attract:
                    ResetScore();
                    break;
            }
        }
    }
}
