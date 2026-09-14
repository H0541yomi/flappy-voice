using UnityEngine;

namespace FlappyVoice.Video
{
    // The selfie feed, drawn as scenery rather than as UI. UI would be simpler - RawImage.uvRect
    // is this whole file - but ShareService captures by layer, so a feed on the UI layer would put
    // the player's face into every shared score card without ever saying so.
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class WebCam : MonoBehaviour
    {
        // WebCamTexture reports 16x16 until its first frame arrives, which is several frames after
        // Play() and never at all if the grant went to a camera that is busy elsewhere.
        private const int UnresolvedSize = 16;

        [SerializeField] private Material feedMaterial;
        [SerializeField] private float zoom = 1f;
        [SerializeField] private Vector2 pan = Vector2.zero;
        [SerializeField] private bool mirror = true;

        private Camera viewCamera;
        private MeshRenderer meshRenderer;
        private WebCamTexture webCamTexture;
        private bool warnedRotated;

        // Applied state, so the per-frame path costs two int compares and returns.
        private int appliedSourceWidth = -1;
        private int appliedSourceHeight = -1;
        private float appliedViewAspect = -1f;
        private float appliedZoom = -1f;
        private Vector2 appliedPan = new Vector2(float.NaN, float.NaN);
        private bool appliedMirror;
        private bool appliedVerticalFlip;

        public bool IsRunning => webCamTexture != null && webCamTexture.isPlaying;

        public void Configure(Camera camera)
        {
            viewCamera = camera;
        }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            // Nothing to show until a frame lands, and an unfed material is a white slab over the
            // whole sky. SceneBuilder authors it off too, so a headless screenshot shows scenery.
            meshRenderer.enabled = false;
        }

        // Called by GameBootstrap once the browser has granted the camera, not from Start:
        // opening a WebCamTexture before the grant hands back a texture that never receives a
        // frame, and nothing about it reports the failure.
        public void Begin(string deviceName)
        {
            if (feedMaterial == null || webCamTexture != null)
            {
                return;
            }

            webCamTexture = string.IsNullOrEmpty(deviceName)
                ? new WebCamTexture()
                : new WebCamTexture(deviceName);
            feedMaterial.mainTexture = webCamTexture;
            webCamTexture.Play();
        }

        private void Update()
        {
            if (webCamTexture == null || viewCamera == null)
            {
                return;
            }

            int sourceWidth = webCamTexture.width;
            int sourceHeight = webCamTexture.height;
            if (sourceWidth <= UnresolvedSize || sourceHeight <= UnresolvedSize)
            {
                return;
            }

            // The source size settles on the first frame but the view aspect does not: a browser
            // window resize or a phone rotation changes it mid-run, and the crop is derived from
            // it, so both have to be watched.
            float viewAspect = viewCamera.aspect;
            bool verticalFlip = webCamTexture.videoVerticallyMirrored;
            if (sourceWidth == appliedSourceWidth && sourceHeight == appliedSourceHeight &&
                viewAspect == appliedViewAspect && zoom == appliedZoom && pan == appliedPan &&
                mirror == appliedMirror && verticalFlip == appliedVerticalFlip)
            {
                return;
            }

            appliedSourceWidth = sourceWidth;
            appliedSourceHeight = sourceHeight;
            appliedViewAspect = viewAspect;
            appliedZoom = zoom;
            appliedPan = pan;
            appliedMirror = mirror;
            appliedVerticalFlip = verticalFlip;

            WarnIfRotated();
            FitQuadToView(viewAspect);

            WebCamCropRect crop = WebCamCrop.Cover(viewAspect, (float)sourceWidth / sourceHeight,
                zoom, pan, mirror, verticalFlip);
            feedMaterial.mainTextureScale = crop.Scale;
            feedMaterial.mainTextureOffset = crop.Offset;

            meshRenderer.enabled = true;
        }

        // The crop assumes the quad is exactly the shape of the view; sized to anything else, the
        // part of it that is on screen is not the rectangle the maths solved for.
        private void FitQuadToView(float viewAspect)
        {
            float viewHeight = viewCamera.orthographicSize * 2f;
            transform.localScale = new Vector3(viewHeight * viewAspect, viewHeight, 1f);
        }

        // A quarter turn would need the quad rotated as well as the crop transposed, and squaring
        // that with a quad sized to the view is more than a decorative background is worth. Web
        // reports 0 here; say so out loud rather than drawing the player sideways in silence.
        private void WarnIfRotated()
        {
            if (warnedRotated || webCamTexture.videoRotationAngle == 0)
            {
                return;
            }

            warnedRotated = true;
            Debug.LogWarning($"[WebCam] device reports videoRotationAngle " +
                $"{webCamTexture.videoRotationAngle}; the feed will be drawn unrotated.");
        }

        // A live WebCamTexture holds the capture stream open, which leaves the browser's camera
        // indicator lit long after anything is drawing it.
        private void OnDisable()
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
            }
        }
    }
}
