using System;
using FlappyVoice.Gameplay;
using FlappyVoice.Video;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    /// <summary>
    /// The in-run pause: a corner button that stops the world, and the parchment behind it
    /// carrying the score so far and the two settings a player can only judge while playing —
    /// how loud they have to sing, and whether their face is behind the pipes.
    /// </summary>
    // Pausing is Time.timeScale, not a fourth GameState. Everything that moves is driven by
    // Time.deltaTime or FixedUpdate, so zero stops all of it in one line, where a Paused state
    // would mean a new branch in PipeSpawner, PlayerController, GameStateManager, QuitButtonUI
    // and LivesUI - five places to get wrong for a screen that only has to hold still.
    //
    // The two settings are raised as events rather than applied here, for the same reason
    // ConsentFlowUI raises its asks: turning the camera back on may need a getUserMedia grant,
    // and GameBootstrap is what owns the routine that asks. It is also where they are persisted,
    // so there is one place that knows what a setting is worth keeping.
    //
    // The camera toggle is a plaque whose label says its own state rather than a switch: the app
    // has one button sprite and no switch art, and "CAMERA: ON" cannot be read backwards the way
    // an unlabelled toggle can.
    //
    // That label is read off the FEED, not off a stored preference, every time the panel opens.
    // A refusal at the consent step raises no event - the flow reports a grant and says nothing
    // about a no - so a preference defaulting to "on" is not evidence of a camera, and the first
    // cut of this said "CAMERA: ON" to every player who had declined. WebCam.IsRunning is the
    // only thing that knows, which is why this holds a reference to it: acting on the toggle
    // still belongs to GameBootstrap, because turning one on may need a getUserMedia grant.
    public sealed class PauseMenuUI : MonoBehaviour
    {
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private QuitButtonUI quitButton;
        [SerializeField] private WebCam webCamBackground;

        [SerializeField] private GameObject pauseButtonRoot;
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI scoreLabel;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Button cameraButton;
        [SerializeField] private TextMeshProUGUI cameraLabel;
        [SerializeField] private Button resumeButton;

        [SerializeField] private string cameraOnLabel = "CAMERA: ON";
        [SerializeField] private string cameraOffLabel = "CAMERA: OFF";

        private bool subscribed;
        // Optimistic between opens: a tap flips it immediately because the grant it may need is
        // several frames away, and the next open reads the feed again and tells the truth.
        private bool cameraEnabled;
        // Set while SetMicSensitivity pushes a stored value in: Slider.value raises
        // onValueChanged whoever wrote it, and a load is not a player changing their mind.
        private bool applyingStoredSettings;

        /// <summary>Whether the panel is up and the world is held still.</summary>
        public bool IsPaused => panelRoot != null && panelRoot.activeSelf;

        /// <summary>Raised when the player moves the slider, with 1 being the most sensitive.</summary>
        public event Action<float> OnMicSensitivityChanged;

        /// <summary>Raised when the player flips the camera plaque.</summary>
        public event Action<bool> OnCameraBackgroundChanged;

        /// <summary>
        /// Replays the edit-time wiring at runtime: every dependency lives in a non-serialized
        /// field here, so the scene save discards them and GameBootstrap hands them back.
        /// </summary>
        public void Configure(GameStateManager state, ScoreManager score, QuitButtonUI quit,
            WebCam webCam)
        {
            Unsubscribe();
            stateManager = state;
            scoreManager = score;
            quitButton = quit;
            webCamBackground = webCam;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        /// <summary>
        /// Shows the stored sensitivity on the slider without reporting it back as a change.
        /// Called once at boot, after whoever owns it has applied it for real.
        /// </summary>
        // Only the slider. The camera plaque has no stored value to show: it reads the feed on
        // every open instead - see the note on the class.
        public void SetMicSensitivity(float sensitivity01)
        {
            applyingStoredSettings = true;
            if (sensitivitySlider != null)
            {
                sensitivitySlider.value = Mathf.Clamp01(sensitivity01);
            }
            applyingStoredSettings = false;
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyState(stateManager != null ? stateManager.State : GameState.Attract);
        }

        private void OnDisable()
        {
            Unsubscribe();
            // Never leave the world stopped behind us. A scene unload with the panel up would
            // otherwise carry timeScale 0 into whatever loads next.
            Resume();
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
            if (stateManager != null)
            {
                stateManager.OnStateChanged += ApplyState;
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(Pause);
            }
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Resume);
            }
            if (cameraButton != null)
            {
                cameraButton.onClick.AddListener(ToggleCamera);
            }
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.AddListener(OnSensitivityMoved);
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }
            if (stateManager != null)
            {
                stateManager.OnStateChanged -= ApplyState;
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(Pause);
            }
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(Resume);
            }
            if (cameraButton != null)
            {
                cameraButton.onClick.RemoveListener(ToggleCamera);
            }
            if (sensitivitySlider != null)
            {
                sensitivitySlider.onValueChanged.RemoveListener(OnSensitivityMoved);
            }
            subscribed = false;
        }

        private void Pause()
        {
            if (stateManager == null || stateManager.State != GameState.Playing || IsPaused)
            {
                return;
            }

            // Rendered on the way up rather than every frame: nothing can score while the world
            // is stopped, so the number cannot go stale behind the panel.
            if (scoreLabel != null && scoreManager != null)
            {
                scoreLabel.SetText(scoreManager.Score.ToString());
            }

            // The feed is the truth, and this is the moment to ask it. A camera the player
            // refused, or one the dev switch never let start, is off however the toggle was
            // last left.
            cameraEnabled = webCamBackground != null && webCamBackground.IsRunning;
            RenderCameraLabel();

            SetPanelShowing(true);
            Time.timeScale = 0f;
        }

        private void Resume()
        {
            SetPanelShowing(false);
            Time.timeScale = 1f;
        }

        // The pause button belongs to a run, so any state change ends the pause with it - which
        // also covers the case nothing else can: a run that was paused when the scene went away.
        private void ApplyState(GameState state)
        {
            if (state != GameState.Playing)
            {
                Resume();
            }
            SetPanelShowing(IsPaused && state == GameState.Playing);
        }

        // The button and the panel are never up together: the panel covers the corner the button
        // sits in, and a pause button over a pause screen is a control with nothing left to do.
        private void SetPanelShowing(bool showing)
        {
            if (panelRoot != null && panelRoot.activeSelf != showing)
            {
                panelRoot.SetActive(showing);
            }

            bool buttonVisible = !showing && stateManager != null &&
                stateManager.State == GameState.Playing;
            if (pauseButtonRoot != null && pauseButtonRoot.activeSelf != buttonVisible)
            {
                pauseButtonRoot.SetActive(buttonVisible);
            }

            // The corner swaps controls rather than emptying: the pause button steps aside and
            // the X takes its place, so a paused run has the same way out every other panel has.
            if (quitButton != null)
            {
                quitButton.SetRunPaused(showing);
            }
        }

        private void ToggleCamera()
        {
            cameraEnabled = !cameraEnabled;
            RenderCameraLabel();
            OnCameraBackgroundChanged?.Invoke(cameraEnabled);
        }

        private void RenderCameraLabel()
        {
            if (cameraLabel != null)
            {
                cameraLabel.SetText(cameraEnabled ? cameraOnLabel : cameraOffLabel);
            }
        }

        private void OnSensitivityMoved(float value)
        {
            if (applyingStoredSettings)
            {
                return;
            }
            OnMicSensitivityChanged?.Invoke(value);
        }
    }
}
