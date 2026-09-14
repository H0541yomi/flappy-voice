using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    // Scrolls the scenery layers against the pipes. Each layer is a row of identical tiles that
    // slides left and wraps by exactly one tile width, so there is no seam to line up and no
    // material state to share between layers.
    public sealed class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] private Transform[] _layers;
        // Fraction of the pipe speed each layer moves at, nearest layer fastest. Index-matched to
        // _layers; a missing entry means the layer does not move.
        [SerializeField] private float[] _layerSpeedFactors;
        [SerializeField] private float[] _layerTileWidths;
        [SerializeField] private float[] _layerHalfSpans;

        private Camera _camera;
        private bool _warnedNarrow;
        private GameConfig _config;
        private PipeSpawner _spawner;
        private GameStateManager _state;

        public void Configure(GameConfig config, PipeSpawner spawner, GameStateManager state)
        {
            _config = config;
            _spawner = spawner;
            _state = state;
        }

        // The tile rows are authored for a fixed worst-case aspect. A Game view wider than that
        // shows the camera's clear colour down the sides, which is easy to mistake for a sprite
        // that failed to load, so say so once instead.
        private void WarnIfTooNarrow()
        {
            if (_warnedNarrow || _layerHalfSpans == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            float viewHalfWidth = _camera.orthographicSize * _camera.aspect;
            for (int i = 0; i < _layerHalfSpans.Length; i++)
            {
                float tile = _layerTileWidths != null && i < _layerTileWidths.Length ? _layerTileWidths[i] : 0f;
                if (_layerHalfSpans[i] - tile < viewHalfWidth)
                {
                    _warnedNarrow = true;
                    Debug.LogWarning($"[ParallaxBackground] view is {viewHalfWidth * 2f:0.0} units " +
                        $"wide but layer {i} only tiles {(_layerHalfSpans[i] - tile) * 2f:0.0}; " +
                        "raise WidestSupportedAspect in SceneBuilder and rebuild the scene.");
                    return;
                }
            }
        }

        private void Update()
        {
            if (_layers == null || _layers.Length == 0)
            {
                return;
            }

            // Only death stops the world. The attract screen keeps drifting so the menu is alive,
            // but scenery moving under a dead bird would read as flight.
            if (_state != null && _state.State == GameState.GameOver)
            {
                return;
            }

            float speed = _spawner != null && _spawner.CurrentSpeed > 0f
                ? _spawner.CurrentSpeed
                : (_config != null ? _config.PipeSpeed : 0f);
            if (speed <= 0f)
            {
                return;
            }

            WarnIfTooNarrow();

            float delta = Time.deltaTime;

            for (int i = 0; i < _layers.Length; i++)
            {
                Transform layer = _layers[i];
                if (layer == null)
                {
                    continue;
                }

                float factor = _layerSpeedFactors != null && i < _layerSpeedFactors.Length
                    ? _layerSpeedFactors[i]
                    : 0f;
                float tile = _layerTileWidths != null && i < _layerTileWidths.Length
                    ? _layerTileWidths[i]
                    : 0f;
                if (factor <= 0f || tile <= 0f)
                {
                    continue;
                }

                Vector3 p = layer.localPosition;
                p.x -= speed * factor * delta;
                // Wrapping by whole tiles rather than resetting to zero keeps the sub-tile
                // remainder, so the scroll never jumps a fraction of a pixel on the wrap frame.
                while (p.x <= -tile)
                {
                    p.x += tile;
                }
                layer.localPosition = p;
            }
        }
    }
}
