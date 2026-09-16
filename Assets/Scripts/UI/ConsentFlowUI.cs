using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyVoice.UI
{
    // The two permission asks, in the order the browser wants them: microphone first because the
    // game is unplayable without it, camera second because it is only decoration. Both are plain
    // UI - nothing here calls getUserMedia. GameBootstrap owns that, and the two answered events
    // below are the hook it (or anything else) subscribes to in order to ask for real.
    //
    // A panel of our own in front of the OS prompt is not politeness. Every browser but desktop
    // Chrome only runs getUserMedia inside a user gesture, and this game reads no input at all,
    // so without a button to press there is no gesture to spend.
    //
    // One panel, not two. The parchment, the title, the sentence and the button row are identical
    // between the steps, so the step changes only the words and which buttons are active; building
    // a second copy of the sign would mean two things to keep in sync and a visible swap as one
    // replaced the other.
    //
    // One button per job, rather than one button that changes job. An earlier cut had the plaque
    // that appears on both steps doing double duty - confirming on the microphone step, refusing
    // on the camera one - which made its handler branch on the step to decide the VERDICT. Three
    // buttons, each with a fixed label, sprite and answer, cost one more object in the row and buy
    // handlers that cannot get the verdict wrong. The row lays out whichever are active, so the
    // lone microphone button centres itself.
    //
    // Both answered events fire only for a GRANT. The microphone step has no refuse button at
    // all (the design gives it one button), and refusing the camera just closes the flow, so
    // neither event is ever raised with false today. They keep the bool so a future decline has
    // somewhere to go; until then, treat "no event" as the refusal.
    public sealed class ConsentFlowUI : MonoBehaviour
    {
        public enum Step
        {
            Microphone,
            Camera,
            Done,
        }

        [SerializeField] private HudUI hud;
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI body;
        [SerializeField] private Button microphoneAcceptButton;
        [SerializeField] private Button cameraDeclineButton;
        [SerializeField] private Button cameraAcceptButton;

        [SerializeField] private string microphoneTitle = "Enable Mic";
        [SerializeField] private string microphoneBody = "this is a sound based game!";
        [SerializeField] private string cameraTitle = "Use Camera?";
        [SerializeField] private string cameraBody = "use your selfie as the background";

        // Raised only when the player asks for the thing, on the frame of the tap, so the gesture
        // is still warm for whatever getUserMedia call the listener makes. There is no verdict to
        // carry: a refusal raises nothing at all, so no event means no.
        public event Action OnMicrophoneRequest;
        public event Action OnCameraRequest;
        public event Action OnCompleted;

        private bool subscribed;

        public Step Current { get; private set; } = Step.Microphone;

        public bool IsShowing => root != null && root.activeSelf;

        /// <summary>
        /// Whether both asks have been answered. Separate from <see cref="IsShowing"/> because a
        /// panel that is off screen has not necessarily been through: anything that must not
        /// interrupt the flow wants this, not the absence of a parchment.
        /// </summary>
        public bool IsComplete => Current == Step.Done;

        // Restarts the flow at the microphone. Called by nothing at present - the authored scene
        // already opens on this step - but a player who declined and changed their mind needs a
        // way back in.
        public void Show()
        {
            GoTo(Step.Microphone);
        }

        public void Hide()
        {
            GoTo(Step.Done);
        }

        public void Configure(HudUI hudUi)
        {
            hud = hudUi;
            ApplyStep();
        }

        private void Awake()
        {
            ApplyStep();
        }

        private void OnEnable()
        {
            Subscribe();
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
            if (microphoneAcceptButton != null)
            {
                microphoneAcceptButton.onClick.AddListener(RequestMicrophonePermission);
            }
            if (cameraDeclineButton != null)
            {
                cameraDeclineButton.onClick.AddListener(OnCameraDeclined);
            }
            if (cameraAcceptButton != null)
            {
                cameraAcceptButton.onClick.AddListener(RequestCameraPermission);
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }
            if (microphoneAcceptButton != null)
            {
                microphoneAcceptButton.onClick.RemoveListener(RequestMicrophonePermission);
            }
            if (cameraDeclineButton != null)
            {
                cameraDeclineButton.onClick.RemoveListener(OnCameraDeclined);
            }
            if (cameraAcceptButton != null)
            {
                cameraAcceptButton.onClick.RemoveListener(RequestCameraPermission);
            }
            subscribed = false;
        }

        private void RequestCameraPermission()
        {
            Answer(OnCameraRequest, Step.Done);
        }

        private void RequestMicrophonePermission()
        {
            Answer(OnMicrophoneRequest, Step.Camera);
        }

        private void OnCameraDeclined()
        {
            GoTo(Step.Done);
        }

        // The listener fires before the step moves, so a handler that calls getUserMedia is still
        // inside the click the browser is counting as the gesture.
        private void Answer(Action request, Step next)
        {
            request?.Invoke();
            GoTo(next);
        }

        private void GoTo(Step step)
        {
            Current = step;
            ApplyStep();
            if (step == Step.Done)
            {
                OnCompleted?.Invoke();
            }
        }

        private void ApplyStep()
        {
            bool microphone = Current == Step.Microphone;
            if (title != null)
            {
                title.text = microphone ? microphoneTitle : cameraTitle;
            }
            if (body != null)
            {
                body.text = microphone ? microphoneBody : cameraBody;
            }
            // Each button carries its own fixed label, so the step only decides which exist. The
            // row lays out whichever are active, so the lone microphone button centres itself.
            if (microphoneAcceptButton != null)
            {
                microphoneAcceptButton.gameObject.SetActive(microphone);
            }
            if (cameraDeclineButton != null)
            {
                cameraDeclineButton.gameObject.SetActive(!microphone);
            }
            if (cameraAcceptButton != null)
            {
                cameraAcceptButton.gameObject.SetActive(!microphone);
            }
            if (root != null)
            {
                root.SetActive(Current != Step.Done);
            }
            if (hud != null)
            {
                hud.SetStartScreenSuppressed(Current != Step.Done);
            }
        }
    }
}
