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

        [Header("Background")]
        [SerializeField] private WebCam webCamBackground;

        private readonly WaitForSeconds micPollDelay = new WaitForSeconds(0.25f);
        private readonly WaitForSeconds micRetryDelay = new WaitForSeconds(0.5f);
        private readonly WaitForSeconds cameraRetryDelay = new WaitForSeconds(0.5f);

        private bool microphoneRoutineRunning;
        private bool microphoneNoticeShown;

        private void Awake()
        {
            ReportMissingReferences();

            if (stateManager != null)
            {
                stateManager.Configure(config);
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
                microphoneNotice.OnDismissed += RequestMicrophone;
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
            }

            if (microphoneNotice != null)
            {
                microphoneNotice.OnDismissed -= RequestMicrophone;
            }
        }

        // Tiny stub to start camera routine, writing this lets the consent flow UI unsubscribe later.
        private void RequestCamera()
        {
            StartCoroutine(StartCameraRoutine());
        }

        // Tiny stub to start microphone routine, writing this lets the consent flow UI unsubscribe later.
        //
        // Two callers now - the consent panel's OK and the notice's dismiss - so it guards against
        // a second loop: on the web the routine retries forever, and a second one would double the
        // request rate against the plugin's own attempt budget. The guard is also what makes the
        // notice show once per deliberate ask rather than once per failed poll.
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
        private IEnumerator ShowMicrophoneNoticeRoutine()
        {
            while (consentFlow != null && consentFlow.IsShowing)
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
