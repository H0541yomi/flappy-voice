using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class Pipe : MonoBehaviour
    {
        private const float BoundsOvershoot = 3f;
        private const float NoteChipWidth = 1.5f;

        [SerializeField] private Transform _topSection;
        [SerializeField] private Transform _bottomSection;
        [SerializeField] private BoxCollider2D _scoreZone;
        [SerializeField] private TMPro.TMP_Text noteLabel;
        [SerializeField] private float _noteLabelOffsetY;
        [SerializeField] private float _width = 1.4f;
        [SerializeField] private float _scoreZoneWidth = 0.25f;
        [SerializeField] private Color _pipeColor = new Color(0.30f, 0.72f, 0.36f, 1f);

        private bool _built;
        private bool _labelMadePassive;

        public float GapCenterY { get; private set; }
        public float GapSize { get; private set; }
        public bool HasScored { get; set; }
        public int NoteOffset { get; private set; }
        public float X => transform.position.x;

        // Widest thing the pipe draws, note chip included, so the spawner can push spawn/despawn far
        // enough past the camera edge that nothing ever pops in or out on screen.
        public float VisualWidth => Mathf.Max(_width, NoteChipWidth);

        private void Awake()
        {
            Build();
        }

        public void Setup(float gapCenterY, float gapSize, float playfieldMinY, float playfieldMaxY)
        {
            Build();

            GapCenterY = gapCenterY;
            GapSize = Mathf.Max(0.1f, gapSize);
            HasScored = false;

            Vector3 root = transform.position;
            root.y = 0f;
            root.z = 0f;
            transform.position = root;

            float halfGap = GapSize * 0.5f;
            float topEdge = playfieldMaxY + BoundsOvershoot;
            float bottomEdge = playfieldMinY - BoundsOvershoot;

            float topStart = gapCenterY + halfGap;
            float topHeight = Mathf.Max(0.05f, topEdge - topStart);
            _topSection.localPosition = new Vector3(0f, topStart + topHeight * 0.5f, 0f);
            _topSection.localScale = new Vector3(_width, topHeight, 1f);

            float bottomStart = gapCenterY - halfGap;
            float bottomHeight = Mathf.Max(0.05f, bottomStart - bottomEdge);
            _bottomSection.localPosition = new Vector3(0f, bottomStart - bottomHeight * 0.5f, 0f);
            _bottomSection.localScale = new Vector3(_width, bottomHeight, 1f);

            _scoreZone.transform.localPosition = new Vector3(0f, gapCenterY, 0f);
            _scoreZone.size = new Vector2(_scoreZoneWidth, GapSize);

            PositionNoteLabel();
        }

        public void SetNote(int semitoneOffset, string noteName)
        {
            NoteOffset = semitoneOffset;

            if (noteLabel == null)
            {
                return;
            }

            MakeLabelPassive();
            noteLabel.text = noteName;
            PositionNoteLabel();
        }

        public void Move(float speed, float deltaTime)
        {
            Vector3 p = transform.position;
            p.x -= speed * deltaTime;
            transform.position = p;
        }

        private void PositionNoteLabel()
        {
            if (noteLabel == null)
            {
                return;
            }

            // The label may sit under a scaled child canvas, so drive it in world space:
            // the pipe root is unscaled and unrotated, making local gap Y a world Y.
            Transform t = noteLabel.transform;
            Vector3 world = t.position;
            world.y = transform.position.y + GapCenterY + _noteLabelOffsetY;
            t.position = world;
        }

        private void MakeLabelPassive()
        {
            if (_labelMadePassive)
            {
                return;
            }

            _labelMadePassive = true;

            noteLabel.raycastTarget = false;

            // A collider here would double-fire the score trigger or kill the player mid-gap.
            Collider2D stray = noteLabel.GetComponent<Collider2D>();
            if (stray != null)
            {
                stray.enabled = false;
            }
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;

            if (_topSection == null)
            {
                _topSection = CreateSection("Top");
            }

            if (_bottomSection == null)
            {
                _bottomSection = CreateSection("Bottom");
            }

            if (_scoreZone == null)
            {
                GameObject zone = new GameObject("ScoreZone");
                zone.transform.SetParent(transform, false);
                _scoreZone = zone.AddComponent<BoxCollider2D>();
                _scoreZone.isTrigger = true;
            }
        }

        private Transform CreateSection(string sectionName)
        {
            GameObject section = new GameObject(sectionName);
            section.transform.SetParent(transform, false);

            SpriteRenderer renderer = section.AddComponent<SpriteRenderer>();
            renderer.sprite = UnitSprite.Get();
            renderer.color = _pipeColor;

            BoxCollider2D collider = section.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = false;

            return section.transform;
        }
    }

    internal static class UnitSprite
    {
        private static Sprite _sprite;

        public static Sprite Get()
        {
            if (_sprite == null)
            {
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();

                _sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            }

            return _sprite;
        }
    }
}
