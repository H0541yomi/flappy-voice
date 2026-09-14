using UnityEngine;

namespace FlappyVoice.UI
{
    // Where the camera has to sit so the playfield stays clear of the tuner strip pinned to the
    // top of the screen.
    //
    // The strip is authored in canvas pixels and the canvas matches on HEIGHT, so it always covers
    // the same FRACTION of the view however wide the device is - but the world height that
    // fraction stands for depends on the camera size, which is what we are solving for. Hence one
    // equation rather than a margin someone eyeballed:
    //
    //     2H(1 - f) = span + bottomMargin + topClearance
    //
    // Below the returned HudBottomY there is room for the whole playfield plus its clearance;
    // above it is the strip's business. HudLayoutTests pins that.
    public static class HudLayout
    {
        // Past this the strip is eating half the screen and there is no camera size that fits a
        // playfield underneath it.
        private const float MaxHudScreenFraction = 0.45f;

        public static void CameraForPlayfield(float playfieldMinY, float playfieldMaxY,
            float bottomMarginUnits, float topClearanceUnits, float hudScreenFraction,
            out float orthographicSize, out float centerY)
        {
            float span = Mathf.Max(0f, playfieldMaxY - playfieldMinY);
            float bottom = Mathf.Max(0f, bottomMarginUnits);
            float top = Mathf.Max(0f, topClearanceUnits);
            float fraction = Mathf.Clamp(hudScreenFraction, 0f, MaxHudScreenFraction);

            orthographicSize = Mathf.Max(1f, (span + bottom + top) / (2f * (1f - fraction)));
            centerY = playfieldMinY - bottom + orthographicSize;
        }

        // World Y of the strip's lower edge, for the same camera.
        public static float HudBottomY(float orthographicSize, float centerY, float hudScreenFraction)
        {
            float fraction = Mathf.Clamp(hudScreenFraction, 0f, MaxHudScreenFraction);
            return centerY + orthographicSize * (1f - (2f * fraction));
        }
    }
}
