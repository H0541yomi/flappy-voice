using TMPro;
using UnityEngine;

namespace FlappyVoice.Gameplay
{
    public sealed class Pipe : MonoBehaviour
    {
        private const float BoundsOvershoot = 3f;

        // A few pixels of the bell's neck, at the 256 px per unit the bells are authored at. The
        // neck is drawn to the tube's exact width, so its outermost column is antialiased and a
        // flush joint would open a hairline of sky at each corner of the seam.
        private const float TubeBellOverlapUnits = 8f / 256f;

        [SerializeField] private Transform _topSection;
        [SerializeField] private Transform _bottomSection;
        // The tube sprite hangs off its section rather than on it. The section is stretched to the
        // pipe's full extent because that is what its collider has to cover, but the tube must stop
        // where the bell's neck begins or it reappears below the flare as a stub in the gap. Sizes
        // here are therefore fractions of the section's stretch, not world units.
        [SerializeField] private Transform _topTube;
        [SerializeField] private Transform _bottomTube;
        [SerializeField] private BoxCollider2D _scoreZone;
        // The letter of the note this gap is centred on, drawn in the opening. A child of the
        // score zone, which Setup already moves to the gap centre, so the letter follows the gap
        // with no per-frame code of its own.
        [SerializeField] private TMP_Text _noteLabel;
        // Children of the pipe root, not of the sections: a section is a 1x1 quad stretched by
        // localScale, and anything parented to it inherits that stretch.
        [SerializeField] private Transform _topBell;
        [SerializeField] private Transform _bottomBell;
        [SerializeField] private float _width = 1.4f;
        [SerializeField] private float _scoreZoneWidth = 0.25f;
        [SerializeField] private Color _pipeColor = Color.white;

        private bool _built;
        private bool _hasScored;
        private float _topBellDepth;
        private float _bottomBellDepth;

        public float GapCenterY { get; private set; }
        public float GapSize { get; private set; }

        // Set by both of PlayerController's contact paths - threading the gap and crashing into
        // the pipe - which makes it exactly the moment the note has been answered one way or the
        // other, and so the moment its letter stops being an instruction and starts being clutter.
        public bool HasScored
        {
            get => _hasScored;
            set
            {
                _hasScored = value;
                if (_noteLabel != null)
                {
                    _noteLabel.enabled = !value;
                }
            }
        }
        public int NoteOffset { get; private set; }
        public float X => transform.position.x;

        // Width of the sections, used by the spawner both to push spawn/despawn past the camera
        // edge and to work out which pipes are sitting on the bird.
        public float Width => _width;

        private void Awake()
        {
            Build();
        }

        // The two bounds are what the sections have to COVER, which is the screen rather than the
        // playfield: the playfield ends below the tuner strip, and a section that stops there ends
        // in mid-air. The caller works them out; here they are just the extent to stretch to.
        public void Setup(float gapCenterY, float gapSize, float coveredMinY, float coveredMaxY)
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
            float topEdge = coveredMaxY + BoundsOvershoot;
            float bottomEdge = coveredMinY - BoundsOvershoot;

            float topStart = gapCenterY + halfGap;
            float topHeight = Mathf.Max(0.05f, topEdge - topStart);
            _topSection.localPosition = new Vector3(0f, topStart + topHeight * 0.5f, 0f);
            _topSection.localScale = new Vector3(_width, topHeight, 1f);

            float bottomStart = gapCenterY - halfGap;
            float bottomHeight = Mathf.Max(0.05f, bottomStart - bottomEdge);
            _bottomSection.localPosition = new Vector3(0f, bottomStart - bottomHeight * 0.5f, 0f);
            _bottomSection.localScale = new Vector3(_width, bottomHeight, 1f);

            // The bell is a length of tube that flares out, drawn at the tube's own width where the
            // two meet, so the tube has to end where the bell's neck begins. Drawn all the way to
            // the gap edge instead, its flat end shows below the flare as a stub - the section
            // still reaches the edge, because the collider is what lines the gap.
            InsetTube(_topTube, topHeight, _topBellDepth, 1f);
            InsetTube(_bottomTube, bottomHeight, _bottomBellDepth, -1f);

            // The bells are authored with their pivot on the flare rim, so placing them exactly on
            // the gap edge is what keeps the flare out of the gap.
            if (_topBell != null)
            {
                _topBell.localPosition = new Vector3(0f, topStart, 0f);
            }

            if (_bottomBell != null)
            {
                _bottomBell.localPosition = new Vector3(0f, bottomStart, 0f);
            }

            _scoreZone.transform.localPosition = new Vector3(0f, gapCenterY, 0f);
            _scoreZone.size = new Vector2(_scoreZoneWidth, GapSize);
        }

        // Which note of the anchored range this gap was centred on. Nothing is drawn for it -
        // the spawner keeps it so a recycled pipe can be asked what it was, and so consecutive
        // gaps can be kept within singing reach of one another.
        public void SetNoteOffset(int semitoneOffset)
        {
            NoteOffset = semitoneOffset;
        }

        /// The letter to sing to thread this gap. Handed in rather than worked out here, because
        /// which letter an offset names depends on the anchored floor - which is not known until
        /// the player's first note, and moves every letter on screen when it lands. Empty until
        /// then: naming a note the pipe is not actually on would teach the dial wrong.
        public void SetNoteLabel(string noteName)
        {
            if (_noteLabel != null)
            {
                _noteLabel.text = noteName;
            }
        }

        /// Pulls a tube's drawn end back inside its bell, so the flare is what the gap is lined
        /// with. Everything is expressed in the section's own units because the section is
        /// non-uniformly scaled: <paramref name="sign"/> is +1 when the gap edge is the section's
        /// lower end, -1 when it is the upper one.
        private static void InsetTube(Transform tube, float sectionHeight, float bellDepth, float sign)
        {
            if (tube == null)
            {
                return;
            }

            float inset = Mathf.Clamp(bellDepth - TubeBellOverlapUnits, 0f, sectionHeight * 0.9f);
            float fraction = inset / Mathf.Max(0.05f, sectionHeight);
            tube.localScale = new Vector3(1f, 1f - fraction, 1f);
            tube.localPosition = new Vector3(0f, sign * fraction * 0.5f, 0f);
        }

        /// How far a bell reaches back up the pipe from the gap edge it sits on. Read off the sprite
        /// rather than written down here, so redrawing the bell moves the tube's end with it, and
        /// measured either side of the pivot so it does not care which end the pivot is on.
        private static float BellDepth(Transform bell)
        {
            SpriteRenderer renderer = bell != null ? bell.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return 0f;
            }

            Bounds bounds = renderer.sprite.bounds;
            return Mathf.Max(bounds.max.y, -bounds.min.y);
        }

        public void Move(float speed, float deltaTime)
        {
            Vector3 p = transform.position;
            p.x -= speed * deltaTime;
            transform.position = p;
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
                _topSection = CreateSection("Top", out _topTube);
            }

            if (_bottomSection == null)
            {
                _bottomSection = CreateSection("Bottom", out _bottomTube);
            }

            _topBellDepth = BellDepth(_topBell);
            _bottomBellDepth = BellDepth(_bottomBell);

            if (_scoreZone == null)
            {
                GameObject zone = new GameObject("ScoreZone");
                zone.transform.SetParent(transform, false);
                _scoreZone = zone.AddComponent<BoxCollider2D>();
                _scoreZone.isTrigger = true;
            }
        }

        /// Fallback for a pipe assembled at runtime rather than loaded from the built prefab: the
        /// section carries the collider and a child carries the tube sprite, which is the split
        /// <see cref="InsetTube"/> needs to shorten one without the other.
        private Transform CreateSection(string sectionName, out Transform tube)
        {
            GameObject section = new GameObject(sectionName);
            section.transform.SetParent(transform, false);

            BoxCollider2D collider = section.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = false;

            GameObject tubeGo = new GameObject("Tube");
            tubeGo.transform.SetParent(section.transform, false);
            SpriteRenderer renderer = tubeGo.AddComponent<SpriteRenderer>();
            renderer.sprite = UnitSprite.Get();
            renderer.color = _pipeColor;

            tube = tubeGo.transform;
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
