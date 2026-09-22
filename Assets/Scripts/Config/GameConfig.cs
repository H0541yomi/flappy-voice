using UnityEngine;

namespace FlappyVoice.Config
{
    [CreateAssetMenu(menuName = "Flappy Voice/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Pipes")]
        // A gap is sized in NOTES, not in world units: it spans this many notes plus the clearance
        // the bird needs to fit between the walls. Widening the pitch range shrinks a semitone on
        // screen, so a fixed world-unit gap would silently change how many notes fit.
        [SerializeField] private int _pipeGapNotes = 1;
        // Room on top of the note span for the bird's body. Ramped down toward the minimum with
        // difficulty; both ends stay wide enough for _pipeGapNotes and too narrow for one more.
        // PipeGapGeometryTests pins that, so retune these two together with it.
        [SerializeField] private float _pipeGapClearanceUnits = 1.375f;
        [SerializeField] private float _minPipeGapClearanceUnits = 1.075f;
        // Speed ramps with difficulty, spacing does not. Pipes stay _pipeSpacingUnits apart in
        // world space for the whole run, so a fully ramped run is one where each gap arrives
        // sooner - it is never one where the gaps bunch up.
        [SerializeField] private float _pipeSpeed = 3f;
        [SerializeField] private float _maxPipeSpeed = 5f;
        // How far apart pipes sit, in world units. Spacing rather than an interval because it
        // is what the player actually sees; the spawner derives its interval from this and the
        // speed.
        [SerializeField] private float _pipeSpacingUnits = 10.4f;
        [SerializeField] private AnimationCurve _difficultyRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        // Pipes passed, not seconds survived: a player who hovers between pipes should not have
        // the gap tighten under them, and two runs that reach the same score should have been
        // equally hard to get there.
        [SerializeField] private int _difficultyRampPipes = 100;
        // Fallbacks only. PipeSpawner derives the real spawn/despawn X from the camera frustum so
        // pipes enter and leave outside the visible edges at every aspect ratio.
        [SerializeField] private float _pipeSpawnXOffset = 5f;
        [SerializeField] private float _pipeDespawnX = -5f;
        [SerializeField] private float _pipeEdgeMarginUnits = 0.8f;
        // The circle that actually kills. Coupled to the gap maths: the gap must stay wide
        // enough for the note it is centred on and too narrow for either neighbour, and both edges
        // of that window move with this radius. PipeGapGeometryTests pins it - retune together.
        [SerializeField] private float _playerBodyRadiusUnits = 0.36f;
        [SerializeField] private int _playerLives = 3;
        // Invincibility normally ends when the pipe that was hit has gone past. This floor holds it
        // open a little longer so the flash reads as feedback rather than a flicker; it is well
        // under the gap between pipes even at the ramped-up speed (~2.1 s), so it can never hand
        // out a free pass through the next one.
        [SerializeField] private float _minInvincibleSec = 0.75f;
        // Backstop for the case where the hit pipe never does go past - recycled and respawned out
        // in front of the bird, or a run that ends first.
        [SerializeField] private float _maxInvincibleSec = 4f;
        [SerializeField] private float _playfieldMinY = -4.5f;
        [SerializeField] private float _playfieldMaxY = 4.5f;

        [Header("Pitch / mapping")]
        [SerializeField] private int _octaveWidthSemitones = 24;
        [SerializeField] private int _anchorCaptureWindowMs = 350;
        [SerializeField] private float _anchorStabilityToleranceSemitones = 1.5f;
        [SerializeField] private float _vocalRangeClampMinHz = 55f;
        [SerializeField] private float _vocalRangeClampMaxHz = 700f;
        [SerializeField] private float _recenterWindowSec = 4f;
        [SerializeField] private float _recenterDriftRatePerSec = 0.35f;
        [SerializeField] private float _recenterEdgeThreshold = 0.15f;

        [Header("Mic / detection")]
        [SerializeField] private float _amplitudeGateRms = 0.001f;
        [SerializeField] private int _pitchBufferSize = 2048;
        [SerializeField] private float _sustainMs = 150f;
        [SerializeField] private float _yinThreshold = 0.15f;

        // A held note is never one frequency: vibrato and the detector's own frame-to-frame
        // noise both land here, and the singer hears one steady note while the screen shakes.
        // This is the ease that hides that, NOT a semitone snap - see VoiceHeightSource.
        [SerializeField] private float _pitchSmoothTimeSec = 0.06f;

        [Header("Movement")]
        [SerializeField] private float _heightSmoothTimeSec = 0.06f;
        [SerializeField] private float _maxVerticalSpeed = 9f;

        [Header("Dev")]
        // Off means the selfie feed is never asked for and never drawn, and the painted parallax
        // sky it normally covers is what you see - the same picture a player who refuses the
        // camera prompt gets. It lives on the config asset rather than on GameBootstrap because
        // the scene is generated: a flag flipped on the component is thrown away the next time
        // SceneBuilder runs, and this one is for recording and debugging without your own face
        // in every frame.
        [SerializeField] private bool _useCameraBackground = true;

        // TEMP: playback rate of the bird's idle cycle, here so it can be dialled in while the
        // game is running. It lives on the config for the same reason the camera switch does -
        // the scene is generated, so a number typed onto PlayerController is thrown away the
        // next time SceneBuilder runs. The cycle is played out and back, so a full sweep takes
        // (frames * 2 - 2) / this seconds: at nine frames and 24, two thirds of a second.
        // Fold the settled value back into a constant and delete this once it is chosen.
        [SerializeField] private float _idleFramesPerSec = 24f;

        public int PipeGapNotes => _pipeGapNotes;
        public float PipeGapClearanceUnits => _pipeGapClearanceUnits;
        public float MinPipeGapClearanceUnits => _minPipeGapClearanceUnits;
        public float PlayerBodyRadiusUnits => _playerBodyRadiusUnits;
        public int PlayerLives => _playerLives;
        public float MinInvincibleSec => _minInvincibleSec;
        public float MaxInvincibleSec => _maxInvincibleSec;
        public float PipeSpeed => _pipeSpeed;
        public float MaxPipeSpeed => _maxPipeSpeed;
        public float PipeSpacingUnits => _pipeSpacingUnits;
        public float SpawnIntervalSec => SpawnIntervalSecAtSpeed(_pipeSpeed);
        public AnimationCurve DifficultyRampCurve => _difficultyRampCurve;
        public int DifficultyRampPipes => _difficultyRampPipes;
        public float PipeSpawnXOffset => _pipeSpawnXOffset;
        public float PipeDespawnX => _pipeDespawnX;
        public float PipeEdgeMarginUnits => _pipeEdgeMarginUnits;
        public float PlayfieldMinY => _playfieldMinY;
        public float PlayfieldMaxY => _playfieldMaxY;

        public int OctaveWidthSemitones => _octaveWidthSemitones;
        public int AnchorCaptureWindowMs => _anchorCaptureWindowMs;
        public float AnchorStabilityToleranceSemitones => _anchorStabilityToleranceSemitones;
        public float VocalRangeClampMinHz => _vocalRangeClampMinHz;
        public float VocalRangeClampMaxHz => _vocalRangeClampMaxHz;
        public float RecenterWindowSec => _recenterWindowSec;
        public float RecenterDriftRatePerSec => _recenterDriftRatePerSec;
        public float RecenterEdgeThreshold => _recenterEdgeThreshold;

        public float AmplitudeGateRms => _amplitudeGateRms;
        public int PitchBufferSize => _pitchBufferSize;
        public float SustainMs => _sustainMs;
        public float YinThreshold => _yinThreshold;
        public float PitchSmoothTimeSec => _pitchSmoothTimeSec;

        public float HeightSmoothTimeSec => _heightSmoothTimeSec;
        public float MaxVerticalSpeed => _maxVerticalSpeed;
        public float IdleFramesPerSec => _idleFramesPerSec;

        // The pause menu's toggle, layered over the authored flag rather than written into it.
        // This is a ScriptableObject ASSET: assigning the serialized field at runtime edits the
        // asset, which in the Editor means a player's toggle gets saved as the shipped default.
        [System.NonSerialized] private bool? _cameraBackgroundOverride;

        public bool UseCameraBackground => _cameraBackgroundOverride ?? _useCameraBackground;

        /// <summary>
        /// Turns the selfie background on or off for this session. WebCam reads the flag every
        /// frame, so the feed opens or is released as soon as this changes.
        /// </summary>
        // Narrows only. The authored flag is the dev kill switch, and a stored preference or a
        // pause-menu tap that could turn the feed back on behind it would make the switch a
        // default rather than an off.
        public void SetUseCameraBackground(bool enabled)
        {
            _cameraBackgroundOverride = enabled && _useCameraBackground;
        }

        public float UnitsPerSemitone
        {
            get
            {
                float span = _playfieldMaxY - _playfieldMinY;
                if (span <= 0f) return 0f;
                return span / Mathf.Max(1, _octaveWidthSemitones);
            }
        }

        // difficulty01 for a running score. Caps at _difficultyRampPipes so the gap stops
        // shrinking there rather than closing on a player good enough to keep going.
        public float DifficultyForPipesPassed(int pipesPassed)
        {
            int over = Mathf.Max(1, _difficultyRampPipes);
            float t = Mathf.Clamp01(Mathf.Max(0, pipesPassed) / (float)over);
            AnimationCurve curve = _difficultyRampCurve;
            return curve != null ? Mathf.Clamp01(curve.Evaluate(t)) : t;
        }

        // Single source of truth for gap height, shared by the spawner at runtime and by the pipe
        // prefab builder at edit time. difficulty01 0 = start of a run, 1 = fully ramped.
        public float PipeGapSizeAtDifficulty(float difficulty01)
        {
            float noteSpan = Mathf.Max(0, _pipeGapNotes - 1) * UnitsPerSemitone;
            float clearance = Mathf.Lerp(_pipeGapClearanceUnits, _minPipeGapClearanceUnits,
                Mathf.Clamp01(difficulty01));
            return Mathf.Max(0.1f, noteSpan + clearance);
        }

        // Single source of truth for pipe speed, on the same ramp as the gap. difficulty01 0 =
        // start of a run, 1 = fully ramped.
        public float PipeSpeedAtDifficulty(float difficulty01)
        {
            return Mathf.Lerp(_pipeSpeed, Mathf.Max(_pipeSpeed, _maxPipeSpeed),
                Mathf.Clamp01(difficulty01));
        }

        // Spacing is the fixed quantity, so the interval falls out of whatever the speed is now.
        // Only good for "how long until the next pipe" - the spawner paces itself by distance.
        public float SpawnIntervalSecAtSpeed(float speed)
        {
            return speed > 0.01f ? _pipeSpacingUnits / speed : 2f;
        }

        public static GameConfig CreateDefault()
        {
            GameConfig c = CreateInstance<GameConfig>();

            c._pipeGapNotes = 1;
            c._pipeGapClearanceUnits = 1.375f;
            c._minPipeGapClearanceUnits = 1.075f;
            c._pipeSpeed = 3f;
            c._maxPipeSpeed = 5f;
            c._pipeSpacingUnits = 10.4f;
            c._difficultyRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            c._difficultyRampPipes = 100;
            c._playerBodyRadiusUnits = 0.36f;
            c._playerLives = 3;
            c._minInvincibleSec = 0.75f;
            c._maxInvincibleSec = 4f;
            c._pipeSpawnXOffset = 5f;
            c._pipeDespawnX = -5f;
            c._pipeEdgeMarginUnits = 0.8f;
            c._playfieldMinY = -4.5f;
            c._playfieldMaxY = 4.5f;

            c._octaveWidthSemitones = 24;
            c._anchorCaptureWindowMs = 350;
            c._anchorStabilityToleranceSemitones = 1.5f;
            c._vocalRangeClampMinHz = 55f;
            c._vocalRangeClampMaxHz = 700f;
            c._recenterWindowSec = 4f;
            c._recenterDriftRatePerSec = 0.35f;
            c._recenterEdgeThreshold = 0.15f;

            c._amplitudeGateRms = 0.001f;
            c._pitchBufferSize = 2048;
            c._sustainMs = 150f;
            c._yinThreshold = 0.15f;
            c._pitchSmoothTimeSec = 0.06f;

            c._heightSmoothTimeSec = 0.06f;
            c._maxVerticalSpeed = 9f;

            c._useCameraBackground = true;

            c.name = "GameConfig (Default)";
            return c;
        }
    }
}
