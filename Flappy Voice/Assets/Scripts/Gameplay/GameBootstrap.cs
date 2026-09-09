using System.Collections;
using System.Text;
using FlappyVoice.Audio;
using FlappyVoice.Config;
using FlappyVoice.Platform;
using FlappyVoice.UI;
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

        [Header("Config")]
        [SerializeField] private GameConfig config;

        [Header("Gameplay")]
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private PipeSpawner pipeSpawner;
        [SerializeField] private AttractPilot attractPilot;
        [SerializeField] private VoiceHeightSource voiceHeightSource;
        [SerializeField] private PlayerController player;

        [Header("Audio")]
        [SerializeField] private PitchTracker pitchTracker;
        [SerializeField] private MicrophoneInput microphoneInput;

        [Header("UI")]
        [SerializeField] private ShareService shareService;
        [SerializeField] private HudUI hud;
        [SerializeField] private TunerBarUI tunerBar;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private EndScreenUI endScreen;

        [Header("Startup")]
        [SerializeField] private bool requestMicrophoneOnStart = true;

        private readonly WaitForSeconds micPollDelay = new WaitForSeconds(0.25f);

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
                pipeSpawner.Configure(config, stateManager);
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

            if (scoreManager != null)
            {
                scoreManager.Configure(stateManager);
            }

            if (player != null)
            {
                player.Configure(config, stateManager, voiceHeightSource, attractPilot);
                player.SetScoreManager(scoreManager);
            }

            if (hud != null)
            {
                hud.Configure(scoreManager, pitchTracker, stateManager);
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

            bool granted = false;
            yield return MicPermission.RequestRoutine(result => granted = result);

            if (!granted)
            {
                Debug.LogWarning("[GameBootstrap] Microphone permission not granted; staying in attract mode.");
                yield break;
            }

            // Android only publishes Microphone.devices some frames after the grant, so a single
            // immediate TryStartRecording would fail on the very platform that just said yes.
            float deadline = Time.realtimeSinceStartup + MicDeviceWaitSec;
            while (Microphone.devices.Length == 0)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning("[GameBootstrap] No microphone device appeared after permission was granted; staying in attract mode.");
                    yield break;
                }

                yield return micPollDelay;
            }

            if (!microphoneInput.TryStartRecording())
            {
                Debug.LogWarning("[GameBootstrap] Microphone permission granted but recording failed to start; staying in attract mode.");
            }
        }

        private void ReportMissingReferences()
        {
            StringBuilder missing = null;

            Collect(ref missing, config, nameof(config));
            Collect(ref missing, stateManager, nameof(stateManager));
            Collect(ref missing, scoreManager, nameof(scoreManager));
            Collect(ref missing, pipeSpawner, nameof(pipeSpawner));
            Collect(ref missing, attractPilot, nameof(attractPilot));
            Collect(ref missing, voiceHeightSource, nameof(voiceHeightSource));
            Collect(ref missing, player, nameof(player));
            Collect(ref missing, pitchTracker, nameof(pitchTracker));
            Collect(ref missing, microphoneInput, nameof(microphoneInput));
            Collect(ref missing, shareService, nameof(shareService));
            Collect(ref missing, hud, nameof(hud));
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
