using UnityEngine;

namespace FlappyVoice.Video
{
    // The texture window a quad should sample, in the form the shader wants: uv * Scale + Offset,
    // which is what Material.mainTextureScale / mainTextureOffset write into _BaseMap_ST.
    public readonly struct WebCamCropRect
    {
        public readonly Vector2 Scale;
        public readonly Vector2 Offset;

        public WebCamCropRect(Vector2 scale, Vector2 offset)
        {
            Scale = scale;
            Offset = offset;
        }
    }

    // A webcam is landscape and the playfield is portrait, so the two never match and something
    // has to give. Letterboxing is not an option here: the feed is scenery, and bars across it
    // read as art that failed to load.
    public static class WebCamCrop
    {
        private static readonly WebCamCropRect Identity =
            new WebCamCropRect(Vector2.one, Vector2.zero);

        // Picks the largest sub-rectangle of the source whose aspect matches the view, so the
        // stretch onto the quad is the same factor on both axes and nothing is distorted. The
        // overflow on the long axis is cropped away.
        //
        // zoom > 1 crops tighter; pan slides the window, in units of the full source.
        public static WebCamCropRect Cover(float viewAspect, float sourceAspect, float zoom,
            Vector2 pan, bool mirrorHorizontal, bool mirrorVertical)
        {
            // WebCamTexture reports 16x16 until its first frame lands, and a camera with no
            // frames reports worse; written as a negated comparison so a NaN aspect is rejected
            // too rather than falling through as "fine".
            if (!(viewAspect > 0f) || !(sourceAspect > 0f))
            {
                return Identity;
            }

            // Only one axis is ever cropped: whichever has surplus. Scaling the other one up to
            // compensate would sample past the edge of the texture, where a clamped sampler
            // smears its last row of pixels across the screen.
            float scaleX = 1f;
            float scaleY = 1f;
            if (sourceAspect > viewAspect)
            {
                scaleX = viewAspect / sourceAspect;
            }
            else
            {
                scaleY = sourceAspect / viewAspect;
            }

            // Below 1 only. A zoom under 1 would want a window bigger than the source.
            float appliedZoom = Mathf.Max(1f, zoom);
            scaleX /= appliedZoom;
            scaleY /= appliedZoom;

            // Centred, then panned. The clamp is what stops a pan from pushing the window off the
            // texture; without a zoom there is no slack and it pins the window back to centre.
            float offsetX = Mathf.Clamp((1f - scaleX) * 0.5f + pan.x, 0f, 1f - scaleX);
            float offsetY = Mathf.Clamp((1f - scaleY) * 0.5f + pan.y, 0f, 1f - scaleY);

            // Same window, traversed backwards: a negative scale makes uv=0 land on the far edge.
            // An unmirrored selfie reads as wrong to everyone who has ever used a mirror.
            return new WebCamCropRect(
                new Vector2(mirrorHorizontal ? -scaleX : scaleX, mirrorVertical ? -scaleY : scaleY),
                new Vector2(mirrorHorizontal ? offsetX + scaleX : offsetX,
                            mirrorVertical ? offsetY + scaleY : offsetY));
        }
    }
}
