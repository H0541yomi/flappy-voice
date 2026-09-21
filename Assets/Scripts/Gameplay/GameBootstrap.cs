using System.Collections;
using System.Text;
using FlappyVoice.Audio;
using FlappyVoice.Config;
using FlappyVoice.Platform;
using FlappyVoice.UI;
using FlappyVoice.Video;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    // Every Configure(...) dependency below is stored in a non-serialized field on its recipient,
    // so edit-time wiring done by SceneBuilder is discarded when the scene asset is written. These
    // serialized references survive the save and replay the whole wiring sequence at runtime.
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const float MicDeviceWaitSec = 3f;
        private const string MicTapHint = "Tap to turn on\nthe microphone";
        private const string MicBlockedHint = "No microphone.\nCheck permissions!";
        // The camera is decoration, so it gets a bounded number of tries and never a hint of its
        // own: nagging for a background would compete with the hint the microphone needs.
        private const int CameraGrantAttempts = 8;
        // The two settings the pause menu owns, kept beside the best score in the browser's own
        // store rather than on the config asset: they are the player's, not the build's.
        private const string MicSensitivityKey = "flappyvoice.micsensitivity";
        private const string CameraBackgroundKey = "flappyvoice.camerabackground";

        [Header("Config")]
        [SerializeField] private GameConfig config;

        [Header("Gameplay")]
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PipeSpawner pipeSpawner;
        [SerializeField] private ParallaxBackground parallaxBackground;
        [SerializeField] private SingingFx singingFx;
        [SerializeField] private LivesManager livesManager;
        [SerializeField] private AttractPilot attractPilot;
        [SerializeField] private VoiceHeightSource voiceHeightSource;
        [SerializeField] private PlayerController player;

        [Header("Audio")]
        [SerializeField] private PitchTracker pitchTracker;
        [SerializeField] private MicrophoneInput microphoneInput;
        [SerializeField] private GameAudio gameAudio;

        [Header("UI")]
        [SerializeField] private ShareService shareService;
        [SerializeField] private HudUI hud;
        [SerializeField] private LivesUI livesUI;
        [SerializeField] private TunerBarUI tunerBar;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private EndScreenUI endScreen;
        // ConsentFlowUI asks for no permission of its own: it raises OnMicrophoneRequest /
        // OnCameraRequest on the frame of the tap, and the routines below do the asking, still
        // inside that tap. This reference is what they subscribe on.
        [SerializeField] private ConsentFlowUI consentFlow;
        // The answer to the ask above, when it does not arrive. Raised from the microphone
        // routine rather than from the panel, because only the routine knows the grant failed.
        [SerializeField] private MicrophoneNoticeUI microphoneNotice;
        [SerializeField] private QuitButtonUI quitButton;
        [SerializeField] private PauseMenuUI pauseMenu;

        [Header("Background")]
        [SerializeField] private WebCam webCamBackground;

        private readonly WaitForSeconds micPollDelay = new WaitForSeconds(0.25f);
        private readonly WaitForSeconds micRetryDelay = new WaitForSeconds(0.5f);
        private readonly WaitForSeconds cameraRetryDelay = new WaitForSeconds(0.5f);

        private bool microphoneRoutineRunning;
        private bool microphoneNoticeShown;
        private bool cameraAsked;
        private bool roundFinished;

        private void Awake()
        {
            ReportMissingReferences();

            if (stateManager != null)
            {
                stateManager.Configure(config);
                stateManager.OnStateChanged += OnGameStateChanged;
            }

            if (pitchTracker != null)
            {
                pitchTracker.Configure(config);
                pitchTracker.SetMicrophoneInput(microphoneInput);
            }

            if (pipeSpawner != null)
            {
                pipeSpawner.Configure(config, stateManager, scoreManager);
                pipeSpawner.SetPlayer(player);
            }

            if (attractPilot != null)
            {
                attractPilot.Configure(config, pipeSpawner);
                if (player != null)
                {
                    attractPilot.SetCharacter(player.transform);
                }
            }

            if (voiceHeightSource != null)
            {
                voiceHeightSource.Configure(config, pitchTracker);
            }

            if (livesManager != null)
            {
                livesManager.Configure(config, stateManager);
                if (player != null)
                {
                    player.SetLivesManager(livesManager);
                }
            }

            if (singingFx != null)
            {
                singingFx.Configure(config, stateManager, voiceHeightSource, player);
            }

            if (parallaxBackground != null)
            {
                parallaxBackground.Configure(config, pipeSpawner, stateManager);
            }

            if (webCamBackground != null)
            {
                webCamBackground.Configure(viewCamera, config);
            }

            if (scoreManager != null)
            {
                scoreManager.Configure(stateManager);
            }

            if (gameAudio != null)
            {
                gameAudio.Configure(stateManager, scoreManager, livesManager);
            }

            if (player != null)
            {
                player.Configure(config, stateManager, voiceHeightSource, attractPilot);
                player.SetScoreManager(scoreManager);
            }

            if (hud != null)
            {
                hud.Configure(scoreManager, stateManager);
            }

            if (livesUI != null)
            {
                livesUI.Configure(livesManager, stateManager);
            }

            if (tunerBar != null)
            {
                tunerBar.Configure(config, voiceHeightSource, pipeSpawner, player, stateManager);
            }

            if (endScreen != null)
            {
                endScreen.Configure(stateManager, scoreManager, shareService);
            }

            if (microphoneNotice != null)
            {
                microphoneNotice.Configure(hud);
                microphoneNotice.OnRetryRequested += RequestMicrophone;
            }

            if (quitButton != null)
            {
                quitButton.Configure(stateManager);
            }

            if (consentFlow != null)
            {
                consentFlow.Configure(hud);
                consentFlow.OnMicrophoneRequest += RequestMicrophone;
                consentFlow.OnCameraRequest += RequestCamera;
                consentFlow.OnCompleted += ReleaseRunStart;
            }

            if (pauseMenu != null)
            {
                pauseMenu.Configure(stateManager, scoreManager, quitButton, webCamBackground);
                pauseMenu.OnMicSensitivityChanged += ApplyMicSensitivity;
                pauseMenu.OnCameraBackgroundChanged += ApplyCameraBackground;
            }

            RestoreStoredSettings();
        }

        // Last in Awake, because both of these land on things Awake has just configured:
        // PitchTracker.Configure reinstates the authored gate, and the camera flag has to be on
        // the config before OnGameStateChanged can read it to decide whether to ask at all.
        private void RestoreStoredSettings()
        {
            float sensitivity = Mathf.Clamp01(
                LocalStore.GetFloat(MicSensitivityKey, PitchTracker.DefaultSensitivity01));

            if (pitchTracker != null)
            {
                pitchTracker.AmplitudeGateRms = PitchTracker.GateRmsForSensitivity(sensitivity);
            }
            if (config != null)
            {
                // Handed the raw preference: SetUseCameraBackground narrows only, so a stored
                // "on" cannot reach past the dev kill switch on the asset.
                config.SetUseCameraBackground(LocalStore.GetInt(CameraBackgroundKey, 1) != 0);
            }
            // The slider is the only control with a stored value to show. The camera plaque
            // reads the live feed each time the panel opens, because a refusal is silent and a
            // preference is not evidence of a camera.
            if (pauseMenu != null)
            {
                pauseMenu.SetMicSensitivity(sensitivity);
            }
        }

        /// <summary>
        /// Applies and stores a new microphone sensitivity. 1 is the most sensitive; the slider
        /// is the calibration knob for a room, which no shipped constant can know in advance.
        /// </summary>
        private void ApplyMicSensitivity(float sensitivity01)
        {
            if (pitchTracker != null)
            {
                pitchTracker.AmplitudeGateRms = PitchTracker.GateRmsForSensitivity(sensitivity01);
            }
            LocalStore.SetFloat(MicSensitivityKey, sensitivity01);
        }

        /// <summary>
        /// Turns the selfie background on or off and remembers the answer. Turning it on may be
        /// the first time the camera has ever been asked for, so it can spend the tap that got
        /// here on the grant - which is why this lives beside the consent routines rather than
        /// on the pause menu.
        /// </summary>
        private void ApplyCameraBackground(bool enabled)
        {
            // Either answer settles it, so the round-two consent parchment does not come back to
            // ask a player who has just told us in the pause menu.
            cameraAsked = true;

            if (config != null)
            {
                config.SetUseCameraBackground(enabled);
            }
            LocalStore.SetInt(CameraBackgroundKey, enabled ? 1 : 0);

            // Grant, not IsRunning: WebCam reopens a feed it already has a grant for on its own,
            // and IsRunning is false on this frame either way because its Update has not run
            // yet. Asking is for the player who refused the camera step, or was never offered
            // it, and has changed their mind - the tap that got here is the gesture it needs.
            if (enabled && webCamBackground != null && !webCamBackground.HasGrant)
            {
                RequestCamera();
            }
        }

        // The camera ask, a round late. It is decoration, so asking before the player has played
        // spends a prompt on someone with no reason to say yes. It goes up on the way back to
        // Attract - after Play Again, before the sing-to-start sign - so the parchment the player
        // just dismissed is not replaced by another one on the same screen.
        //
        // Once. A browser refusal sticks for the site, so a second ask would be a parchment that
        // cannot change anything - and an accepted camera is already running.
        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                roundFinished = true;
                return;
            }
            if (state != GameState.Attract || !roundFinished || cameraAsked || consentFlow == null)
            {
                return;
            }
            // Nothing to ask for if the feed is switched off or absent: StartCameraRoutine would
            // walk straight back out, and the player would have answered a question about a
            // background that was never going to be drawn.
            if (webCamBackground == null || (config != null && !config.UseCameraBackground))
            {
                return;
            }

            cameraAsked = true;
            // A run starts on a sung note, and by now the microphone is live, so the panel's dim
            // is no defence: without this the next note the player makes - including one they are
            // already holding - would start a round behind the parchment.
            stateManager.SetRunStartHeld(true);
            consentFlow.ShowCameraStep();
        }

        // Both answers land here - OnCompleted is raised by the grant and the refusal alike - and
        // so does the microphone step's own Done at boot, where no run is being held anyway.
        private void ReleaseRunStart()
        {
            if (stateManager != null)
            {
                stateManager.SetRunStartHeld(false);
            }
        }

        // Subscribing handed ConsentFlowUI a reference to this object, and it outlives a scene
        // change, so the pair has to be undone or the panel would call into a destroyed bootstrap.
        // Both are named methods rather than lambdas for exactly this reason - there is nothing
        // for -= to match on an anonymous one.
        private void OnDestroy()
        {
            if (consentFlow != null)
            {
                consentFlow.OnMicrophoneRequest -= RequestMicrophone;
                consentFlow.OnCameraRequest -= RequestCamera;
                consentFlow.OnCompleted -= ReleaseRunStart;
            }

            if (stateManager != null)
            {
                stateManager.OnStateChanged -= OnGameStateChanged;
            }

            if (microphoneNotice != null)
            {
                microphoneNotice.OnRetryRequested -= RequestMicrophone;
            }

            if (pauseMenu != null)
            {
                pauseMenu.OnMicSensitivityChanged -= ApplyMicSensitivity;
                pauseMenu.OnCameraBackgroundChanged -= ApplyCameraBackground;
            }
        }

        // Tiny stub to start camera routine, writing this lets the consent flow UI unsubscribe later.
        private void RequestCamera()
        {
            StartCoroutine(StartCameraRoutine());
        }

        // Tiny stub to start microphone routine, writing this lets the consent flow UI unsubscribe later.
        //
        // Two callers now - the consent panel's OK and the notice's OK - so it guards against a
        // second loop: on the web the routine retries forever, and a second one would double the
        // request rate against the plugin's own attempt budget. The guard is also what makes the
        // notice show once per deliberate ask rather than once per failed poll.
        //
        // The notice is left showing across this. Asking again is not an answer, so the panel only
        // comes down where the grant actually lands, below; a second refusal re-runs the loop with
        // the parchment already up and ShowMicrophoneNoticeRoutine's Show() is then a no-op.
        private void RequestMicrophone()
        {
            if (microphoneRoutineRunning)
            {
                return;
            }
            microphoneRoutineRunning = true;
            microphoneNoticeShown = false;
            StartCoroutine(MicrophoneRoutine());
        }

        private IEnumerator MicrophoneRoutine()
        {
            yield return StartMicrophoneRoutine();
            microphoneRoutineRunning = false;
        }

        // Deferred rather than shown outright: this is raised while the consent flow may still be
        // on its camera step, and the two are the same parchment. Stacking them reads as one
        // broken sign, so the notice waits its turn.
        //
        // Waits on IsComplete rather than on the parchment being gone. The grant is asked for from
        // inside the microphone step's tap, so a browser that refuses instantly raises this while
        // the flow is mid-transition to the camera step; a wait that only watched IsShowing could
        // be satisfied by that gap and put the notice up over the camera ask.
        private IEnumerator ShowMicrophoneNoticeRoutine()
        {
            while (consentFlow != null && !consentFlow.IsComplete)
            {
                yield return null;
            }
            if (microphoneNotice != null)
            {
                microphoneNotice.Show();
            }
        }

        // Fire-and-forget so the retry loop above keeps polling while the notice waits out the
        // consent flow; the flag makes every pass after the first a no-op.
        private void ReportMicrophoneMissing()
        {
            if (microphoneNoticeShown || microphoneNotice == null)
            {
                return;
            }
            microphoneNoticeShown = true;
            StartCoroutine(ShowMicrophoneNoticeRoutine());
        }

        // Requests Microphone permissions in the browser.
        private IEnumerator StartMicrophoneRoutine()
        {
            if (microphoneInput == null)
            {
                Debug.LogError("[GameBootstrap] MicrophoneInput reference missing; the game cannot leave attract mode.");
                yield break;
            }

            while (true)
            {
                bool granted = false;
                yield return MicPermission.RequestRoutine(result => granted = result);

                if (granted)
                {
                    // Android only publishes its device list some frames after the grant, so a
                    // single immediate TryStartRecording would fail on the very platform that
                    // just said yes.
                    float deadline = Time.realtimeSinceStartup + MicDeviceWaitSec;
                    while (microphoneInput.DeviceCount == 0 && Time.realtimeSinceStartup < deadline)
                    {
                        yield return micPollDelay;
                    }

                    if (microphoneInput.DeviceCount > 0 && microphoneInput.TryStartRecording())
                    {
                        // Nothing about the camera here. The two grants are independent: the
                        // consent panel asks for each one separately and each tap is its own user
                        // gesture, so chaining the camera onto a microphone grant would spend a
                        // prompt the player has not agreed to yet.
                        SetStartHint(null);
                        // The one place the notice comes down: its own OK only asks again, so
                        // recording starting is the only thing that can have answered it.
                        if (microphoneNotice != null)
                        {
                            microphoneNotice.Hide();
                        }
                        yield break;
                    }
                }

                if (!MicPermission.RetriesOnUserGesture)
                {
                    Debug.LogWarning("[GameBootstrap] Microphone unavailable; staying in attract mode.");
                    SetStartHint(MicBlockedHint);
                    ReportMicrophoneMissing();
                    yield break;
                }

                // Every browser but desktop Chrome only runs getUserMedia inside a user gesture,
                // and this game reads no input at all, so ask for the tap that the jslib bridge is
                // already listening for and try again once it has had one.
                SetStartHint(MicTapHint);
                ReportMicrophoneMissing();
                yield return micRetryDelay;
            }
        }

        // Runs after the microphone is recording, because the tap that granted the microphone is
        // the same user gesture the browser wants for the camera, and it is still warm here.
        private IEnumerator StartCameraRoutine()
        {
            if (webCamBackground == null)
            {
                yield break;
            }

            // The dev toggle has to stop the prompt, not just the drawing: asking for a camera
            // you have already decided not to show is the one part a player would notice, and on
            // the browser a refusal then sticks.
            if (config != null && !config.UseCameraBackground)
            {
                Debug.Log("[GameBootstrap] UseCameraBackground is off in GameConfig; the painted sky stays.");
                yield break;
            }

            for (int attempt = 0; attempt < CameraGrantAttempts; attempt++)
            {
                bool granted = false;
                yield return CameraPermission.RequestRoutine(result => granted = result);

                if (granted)
                {
                    string deviceName = null;
                    yield return CameraPermission.WaitForCameraDevice(device => deviceName = device);

                    if (deviceName != null)
                    {
                        webCamBackground.Begin(deviceName);
                        yield break;
                    }
                }

                // Off the web a refusal is final and only a trip to Settings undoes it, so there
                // is nothing a retry could pick up.
                if (!CameraPermission.RetriesOnUserGesture)
                {
                    break;
                }

                yield return cameraRetryDelay;
            }

            Debug.Log("[GameBootstrap] No camera; the painted sky stays.");
        }

        private void SetStartHint(string hint)
        {
            if (hud != null) hud.SetStartHint(hint);
        }

        private void ReportMissingReferences()
        {
            StringBuilder missing = null;

            Collect(ref missing, config, nameof(config));
            Collect(ref missing, stateManager, nameof(stateManager));
            Collect(ref missing, scoreManager, nameof(scoreManager));
            Collect(ref missing, pipeSpawner, nameof(pipeSpawner));
            Collect(ref missing, parallaxBackground, nameof(parallaxBackground));
            Collect(ref missing, webCamBackground, nameof(webCamBackground));
            Collect(ref missing, singingFx, nameof(singingFx));
            Collect(ref missing, livesManager, nameof(livesManager));
            Collect(ref missing, attractPilot, nameof(attractPilot));
            Collect(ref missing, voiceHeightSource, nameof(voiceHeightSource));
            Collect(ref missing, player, nameof(player));
            Collect(ref missing, pitchTracker, nameof(pitchTracker));
            Collect(ref missing, microphoneInput, nameof(microphoneInput));
            Collect(ref missing, gameAudio, nameof(gameAudio));
            Collect(ref missing, shareService, nameof(shareService));
            Collect(ref missing, hud, nameof(hud));
            Collect(ref missing, livesUI, nameof(livesUI));
            Collect(ref missing, tunerBar, nameof(tunerBar));
            Collect(ref missing, viewCamera, nameof(viewCamera));
            Collect(ref missing, endScreen, nameof(endScreen));
            Collect(ref missing, consentFlow, nameof(consentFlow));
            Collect(ref missing, microphoneNotice, nameof(microphoneNotice));
            Collect(ref missing, quitButton, nameof(quitButton));
            Collect(ref missing, pauseMenu, nameof(pauseMenu));

            if (missing != null)
            {
                Debug.LogError($"[GameBootstrap] unassigned references ({missing}); rebuild the scene via Flappy Voice/Build Game Scene.");
            }
        }

        private static void Collect(ref StringBuilder missing, Object reference, string fieldName)
        {
            if (reference != null)
            {
                return;
            }

            if (missing == null)
            {
                missing = new StringBuilder(fieldName);
                return;
            }

            missing.Append(", ").Append(fieldName);
        }
    }
}
