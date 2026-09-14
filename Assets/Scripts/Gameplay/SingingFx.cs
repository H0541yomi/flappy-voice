using FlappyVoice.Config;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    // Music notes trailing off the bird's beak while it sings. Driven off the same voice gate as
    // the bird's singing pose, so the two cannot disagree.
    public sealed class SingingFx : MonoBehaviour
    {
        [SerializeField] private Sprite[] _noteSprites;
        [SerializeField] private float _noteIntervalSec = 0.28f;
        [SerializeField] private float _noteLifetimeSec = 1.1f;
        [SerializeField] private float _noteRiseUnitsPerSec = 1.6f;
        [SerializeField] private float _noteDriftUnitsPerSec = 0.7f;
        [SerializeField] private float _noteScale = 0.9f;
        [SerializeField] private int _notePoolSize = 12;
        // Where the note leaves the bird, relative to its centre: at the beak, which sits
        // forward and slightly above the middle of the body.
        [SerializeField] private Vector2 _beakOffset = new Vector2(0.42f, 0.12f);

        private GameStateManager _state;
        private VoiceHeightSource _voice;
        private PlayerController _player;

        private Transform[] _notes;
        private SpriteRenderer[] _noteRenderers;
        private float[] _noteAge;
        private int _nextNote;
        private float _noteTimer;
        private int _noteVariant;

        public void Configure(GameConfig config, GameStateManager state, VoiceHeightSource voice,
            PlayerController player)
        {
            _state = state;
            _voice = voice;
            _player = player;
            EnsurePool();
        }

        private void EnsurePool()
        {
            if (_notes != null || _noteSprites == null || _noteSprites.Length == 0)
            {
                return;
            }

            _notes = new Transform[_notePoolSize];
            _noteRenderers = new SpriteRenderer[_notePoolSize];
            _noteAge = new float[_notePoolSize];

            for (int i = 0; i < _notePoolSize; i++)
            {
                GameObject go = new GameObject("Note" + i);
                go.transform.SetParent(transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _noteSprites[0];
                sr.sortingOrder = 9;
                go.transform.localScale = Vector3.one * _noteScale;
                go.SetActive(false);
                _notes[i] = go.transform;
                _noteRenderers[i] = sr;
                _noteAge[i] = -1f;
            }
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            bool singing = _voice != null && _voice.IsActive
                && (_state == null || _state.State == GameState.Playing);

            AgeNotes(delta);

            if (singing)
            {
                _noteTimer += delta;
                if (_noteTimer >= _noteIntervalSec)
                {
                    _noteTimer = 0f;
                    Emit();
                }
            }
            else
            {
                _noteTimer = _noteIntervalSec;
            }

        }

        private void AgeNotes(float delta)
        {
            if (_notes == null)
            {
                return;
            }

            for (int i = 0; i < _notes.Length; i++)
            {
                if (_noteAge[i] < 0f)
                {
                    continue;
                }

                _noteAge[i] += delta;
                float t = _noteAge[i] / Mathf.Max(0.01f, _noteLifetimeSec);
                if (t >= 1f)
                {
                    _noteAge[i] = -1f;
                    _notes[i].gameObject.SetActive(false);
                    continue;
                }

                _notes[i].localPosition += new Vector3(_noteDriftUnitsPerSec * delta,
                    _noteRiseUnitsPerSec * delta, 0f);

                Color c = _noteRenderers[i].color;
                // Fades over the back half only, so a note is fully solid while it is still
                // close enough to the bird to be read as coming from it.
                c.a = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
                _noteRenderers[i].color = c;
            }
        }

        private void Emit()
        {
            if (_notes == null || _player == null)
            {
                return;
            }

            int i = _nextNote;
            _nextNote = (_nextNote + 1) % _notes.Length;

            _noteVariant = (_noteVariant + 1) % _noteSprites.Length;
            _noteRenderers[i].sprite = _noteSprites[_noteVariant];
            _noteRenderers[i].color = Color.white;

            Vector3 from = _player.transform.position;
            _notes[i].position = new Vector3(
                from.x + _beakOffset.x + Random.Range(-0.06f, 0.06f),
                from.y + _beakOffset.y + Random.Range(-0.06f, 0.06f), 0f);
            _noteAge[i] = 0f;
            _notes[i].gameObject.SetActive(true);
        }

    }
}
