using UnityEngine;

namespace FlappyVoice.Config
{
    [CreateAssetMenu(menuName = "Flappy Voice/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Pipes")]
        // A gap is sized in NOTES, not in world units: it spans this many adjacent notes plus the
        // clearance the bird needs to fit between the walls. Widening the pitch range shrinks a
        // semitone on screen, so a fixed world-unit gap would silently change how many notes fit.
        [SerializeField] private int _pipeGapNotes = 2;
        // Room on top of the note span for the bird's body. Ramped down toward the minimum with
        // difficulty; both ends stay wide enough for _pipeGapNotes and too narrow for one more.
        // PipeGapGeometryTests pins that, so retune these two together with it.
        [SerializeField] private float _pipeGapClearanceUnits = 1.375f;
        [SerializeField] private float _minPipeGapClearanceUnits = 1.075f;
        [SerializeField] private float _pipeSpeed = 3f;
        [SerializeField] private float _maxPipeSpeed = 5.5f;
        [SerializeField] private float _spawnIntervalSec = 2f;
        [SerializeField] private float _minSpawnIntervalSec = 1.1f;
        [SerializeField] private AnimationCurve _difficultyRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float _difficultyRampDurationSec = 90f;
        // Fallbacks only. PipeSpawner derives the real spawn/despawn X from the camera frustum so
        // pipes enter and leave outside the visible edges at every aspect ratio.
        [SerializeField] private float _pipeSpawnXOffset = 5f;
        [SerializeField] private float _pipeDespawnX = -5f;
        [SerializeField] private float _pipeEdgeMarginUnits = 0.8f;
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
        [SerializeField] private float _amplitudeGateRms = 0.015f;
        [SerializeField] private int _pitchBufferSize = 2048;
        [SerializeField] private float _sustainMs = 80f;
        [SerializeField] private float _yinThreshold = 0.15f;

        [Header("Movement")]
        [SerializeField] private float _heightSmoothTimeSec = 0.06f;
        [SerializeField] private float _maxVerticalSpeed = 9f;

        public int PipeGapNotes => _pipeGapNotes;
        public float PipeGapClearanceUnits => _pipeGapClearanceUnits;
        public float MinPipeGapClearanceUnits => _minPipeGapClearanceUnits;
        public float PipeSpeed => _pipeSpeed;
        public float MaxPipeSpeed => _maxPipeSpeed;
        public float SpawnIntervalSec => _spawnIntervalSec;
        public float MinSpawnIntervalSec => _minSpawnIntervalSec;
        public AnimationCurve DifficultyRampCurve => _difficultyRampCurve;
        public float DifficultyRampDurationSec => _difficultyRampDurationSec;
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

        public float HeightSmoothTimeSec => _heightSmoothTimeSec;
        public float MaxVerticalSpeed => _maxVerticalSpeed;

        public float UnitsPerSemitone
        {
            get
            {
                float span = _playfieldMaxY - _playfieldMinY;
                if (span <= 0f) return 0f;
                return span / Mathf.Max(1, _octaveWidthSemitones);
            }
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

        public static GameConfig CreateDefault()
        {
            GameConfig c = CreateInstance<GameConfig>();

            c._pipeGapNotes = 2;
            c._pipeGapClearanceUnits = 1.375f;
            c._minPipeGapClearanceUnits = 1.075f;
            c._pipeSpeed = 3f;
            c._maxPipeSpeed = 5.5f;
            c._spawnIntervalSec = 2f;
            c._minSpawnIntervalSec = 1.1f;
            c._difficultyRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            c._difficultyRampDurationSec = 90f;
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

            c._amplitudeGateRms = 0.015f;
            c._pitchBufferSize = 2048;
            c._sustainMs = 80f;
            c._yinThreshold = 0.15f;

            c._heightSmoothTimeSec = 0.06f;
            c._maxVerticalSpeed = 9f;

            c.name = "GameConfig (Default)";
            return c;
        }
    }
}
