using UnityEngine;

namespace FlappyVoice.Config
{
    [CreateAssetMenu(menuName = "Flappy Voice/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Pipes")]
        [SerializeField] private float _pipeGapSize = 3.2f;
        [SerializeField] private float _minPipeGapSize = 2.1f;
        [SerializeField] private float _pipeSpeed = 3f;
        [SerializeField] private float _maxPipeSpeed = 5.5f;
        [SerializeField] private float _spawnIntervalSec = 2f;
        [SerializeField] private float _minSpawnIntervalSec = 1.1f;
        [SerializeField] private AnimationCurve _difficultyRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float _difficultyRampDurationSec = 90f;
        [SerializeField] private float _pipeSpawnXOffset = 5f;
        [SerializeField] private float _pipeDespawnX = -5f;
        [SerializeField] private float _playfieldMinY = -4.5f;
        [SerializeField] private float _playfieldMaxY = 4.5f;

        [Header("Pitch / mapping")]
        [SerializeField] private int _octaveWidthSemitones = 12;
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

        [Header("Flap")]
        [SerializeField] private float _flapAmplitudeUnits = 0.35f;
        [SerializeField] private float _flapCyclesPerSec = 2.5f;

        public float PipeGapSize => _pipeGapSize;
        public float MinPipeGapSize => _minPipeGapSize;
        public float PipeSpeed => _pipeSpeed;
        public float MaxPipeSpeed => _maxPipeSpeed;
        public float SpawnIntervalSec => _spawnIntervalSec;
        public float MinSpawnIntervalSec => _minSpawnIntervalSec;
        public AnimationCurve DifficultyRampCurve => _difficultyRampCurve;
        public float DifficultyRampDurationSec => _difficultyRampDurationSec;
        public float PipeSpawnXOffset => _pipeSpawnXOffset;
        public float PipeDespawnX => _pipeDespawnX;
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
        public float FlapAmplitudeUnits => _flapAmplitudeUnits;
        public float FlapCyclesPerSec => _flapCyclesPerSec;

        public static GameConfig CreateDefault()
        {
            GameConfig c = CreateInstance<GameConfig>();

            c._pipeGapSize = 3.2f;
            c._minPipeGapSize = 2.1f;
            c._pipeSpeed = 3f;
            c._maxPipeSpeed = 5.5f;
            c._spawnIntervalSec = 2f;
            c._minSpawnIntervalSec = 1.1f;
            c._difficultyRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            c._difficultyRampDurationSec = 90f;
            c._pipeSpawnXOffset = 5f;
            c._pipeDespawnX = -5f;
            c._playfieldMinY = -4.5f;
            c._playfieldMaxY = 4.5f;

            c._octaveWidthSemitones = 12;
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
            c._flapAmplitudeUnits = 0.35f;
            c._flapCyclesPerSec = 2.5f;

            c.name = "GameConfig (Default)";
            return c;
        }
    }
}
