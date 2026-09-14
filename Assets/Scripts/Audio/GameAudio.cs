using FlappyVoice.Gameplay;
using UnityEngine;

namespace FlappyVoice.Audio
{
    // Every audible event in the game, in one place: two one-shots, and nothing else. The clips
    // in Assets/Audio are synthesised placeholders (Tools/make-placeholder-audio.py) - dropping
    // real files onto these two fields is the whole swap, no code involved.
    //
    // There is deliberately NO music anywhere, not even on the game-over screen. The game listens
    // to the microphone the whole time it is on screen, so anything coming out of the speaker
    // feeds straight back into the pitch detector.
    public sealed class GameAudio : MonoBehaviour
    {
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private LivesManager livesManager;

        [SerializeField] private AudioSource sfxSource;

        [SerializeField] private AudioClip scoreSfx;
        [SerializeField] private AudioClip crashSfx;

        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.85f;

        private int lastScore;
        private int lastLives;
        private bool subscribed;

        public void Configure(GameStateManager state, ScoreManager score, LivesManager lives)
        {
            Unsubscribe();
            stateManager = state;
            scoreManager = score;
            livesManager = lives;
            lastScore = scoreManager != null ? scoreManager.Score : 0;
            lastLives = livesManager != null ? livesManager.Lives : 0;

            if (isActiveAndEnabled)
            {
                Subscribe();
                ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
            }
        }

        private void OnEnable()
        {
            Subscribe();
            lastScore = scoreManager != null ? scoreManager.Score : 0;
            lastLives = livesManager != null ? livesManager.Lives : 0;
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

            subscribed = true;
            if (stateManager != null) stateManager.OnStateChanged += ApplyState;
            if (scoreManager != null) scoreManager.OnScoreChanged += HandleScoreChanged;
            if (livesManager != null) livesManager.OnLivesChanged += HandleLivesChanged;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
            if (stateManager != null) stateManager.OnStateChanged -= ApplyState;
            if (scoreManager != null) scoreManager.OnScoreChanged -= HandleScoreChanged;
            if (livesManager != null) livesManager.OnLivesChanged -= HandleLivesChanged;
        }

        private void ApplyState(GameState state)
        {
            // Nothing to play on GameOver: the hit that ended the run already spent a life, and
            // HandleLivesChanged sounded it. Firing the crash again would double it on the last
            // hit only.
            if (state == GameState.Attract)
            {
                lastScore = 0;
            }
        }

        // Every pipe the bird actually hits, not just the fatal one: a survivable hit spends a
        // life, so a DECREASE here is exactly the set of collisions that counted. Invincible
        // contacts never reach LivesManager, so they correctly make no sound.
        private void HandleLivesChanged(int value)
        {
            // ResetLives raises this too, restoring the count - only losing one is a hit.
            if (value < lastLives)
            {
                PlayOneShot(crashSfx);
            }

            lastLives = value;
        }

        // Fires as the bird passes the score trigger, which sits at the centre of the gap - so the
        // sound lands as it crosses the middle of the pipe, not as it enters or leaves.
        private void HandleScoreChanged(int value)
        {
            // ResetScore raises this too, with 0. Only an increase is a point.
            if (value > lastScore)
            {
                PlayOneShot(scoreSfx);
            }

            lastScore = value;
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (sfxSource == null || clip == null)
            {
                return;
            }

            // PlayOneShot rather than Play: points can land close enough together that the previous
            // blip is still ringing, and cutting it off makes a fast run sound broken.
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }
}
