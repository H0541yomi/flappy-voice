using FlappyVoice.Gameplay;
using UnityEngine;

namespace FlappyVoice.Audio
{
    // Every audible event in the game, in one place: a music bed that follows the game state and
    // two one-shots. The clips in Assets/Audio are synthesised placeholders (Tools/make-placeholder-
    // audio.py) - dropping real files onto these four fields is the whole swap, no code involved.
    //
    // Kept deliberately mute-able as a unit: this game listens to the microphone while it plays, so
    // music out of a phone speaker is going into the pitch detector. That is survivable at these
    // volumes and with the amplitude gate, but it is the reason the music is quiet by default.
    public sealed class GameAudio : MonoBehaviour
    {
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private ScoreManager scoreManager;

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        // The start screen and the run share this one, by design: the handoff into a run should not
        // be audible as a change of track.
        [SerializeField] private AudioClip gameMusic;
        [SerializeField] private AudioClip gameOverMusic;
        [SerializeField] private AudioClip scoreSfx;
        [SerializeField] private AudioClip crashSfx;

        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.85f;

        private int lastScore;
        private bool subscribed;

        public void Configure(GameStateManager state, ScoreManager score)
        {
            Unsubscribe();
            stateManager = state;
            scoreManager = score;
            lastScore = scoreManager != null ? scoreManager.Score : 0;

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
        }

        private void ApplyState(GameState state)
        {
            if (state == GameState.GameOver)
            {
                PlayOneShot(crashSfx);
                PlayMusic(gameOverMusic);
                return;
            }

            // Attract and Playing are the same track, so this is a no-op across the handoff.
            PlayMusic(gameMusic);

            if (state == GameState.Attract)
            {
                lastScore = 0;
            }
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

        private void PlayMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null)
            {
                return;
            }

            // Restarting a track that is already the right one would put a seam in the music at
            // exactly the moment the player sang, which reads as a glitch rather than a cue.
            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                return;
            }

            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.Play();
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
