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

        [Header("Background")]
        [SerializeField] private WebCam webCamBackground;

        [Header("Startup")]
        [SerializeField] private bool requestMicrophoneOnStart = true;
        [SerializeField] private bool requestCameraOnStart = true;

        private readonly WaitForSeconds micPollDelay = new WaitForSeconds(0.25f);
        private readonly WaitForSeconds micRetryDelay = new WaitForSeconds(0.5f);
        private readonly WaitForSeconds cameraRetryDelay = new WaitForSeconds(0.5f);

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
                webCamBackground.Configure(viewCamera);
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
        }

        private void Start()
        {
            if (requestMicrophoneOnStart)
            {
                StartCoroutine(StartMicrophoneRoutine());
            }
        }

        // Runs as a coroutine so attract mode keeps playing behind the OS permission prompt
        // (PRD 11) and the first attract -> play transition is never blocked on a modal wait.
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
                        SetStartHint(null);
                        // Only now, and never bundled into one getUserMedia with the microphone:
                        // a combined prompt is all-or-nothing, so a player who simply does not
                        // want their face on screen would lose the microphone and the game with
                        // it. Two prompts, mic first, is the cheaper trade.
                        if (requestCameraOnStart)
                        {
                            StartCoroutine(StartCameraRoutine());
                        }
                        yield break;
                    }
                }

                if (!MicPermission.RetriesOnUserGesture)
                {
                    Debug.LogWarning("[GameBootstrap] Microphone unavailable; staying in attract mode.");
                    SetStartHint(MicBlockedHint);
                    yield break;
                }

                // Every browser but desktop Chrome only runs getUserMedia inside a user gesture,
                // and this game reads no input at all, so ask for the tap that the jslib bridge is
                // already listening for and try again once it has had one.
                SetStartHint(MicTapHint);
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
