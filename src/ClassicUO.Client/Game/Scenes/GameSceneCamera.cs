// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes
{
    // Sub-pixel camera (docs/CAMERA_PLAN.md). The camera follows the player in whole world pixels;
    // zoomed in, one world pixel is several screen pixels and walking scrolls in visible jumps. The
    // world is shifted by the dropped fraction of the player's step and the player is drawn shifted
    // back by the same amount, so the player stays centered and the world scrolls smoothly.
    internal partial class GameScene
    {
        // below this magnification a world pixel is not larger than a screen pixel: nothing to gain
        private const float SUB_PIXEL_MIN_SCALE = 1.01f;

        private Vector2 _cameraSubPixel;

        // set while outline copies of a mobile are drawn: their lights are not collected
        public static bool SuppressLights { get; set; }

        // world pixel of the world target's origin this frame (the scene draw offset); for effects
        // that remember world positions (docs/EFFECTS_PLAN.md)
        public static Point DrawOffset { get; private set; }

        // read by MobileView: mobiles are drawn with their own sub-pixel step (GetSubPixel)
        public static bool SubPixelCameraActive { get; private set; }

        // The part of a mobile's movement offset that drawing in whole pixels drops, in world pixels.
        // Same rounding as the camera (GetViewPort) and MobileView: (int) of X and of Y - Z.
        public static Vector2 GetSubPixel(Vector3 offset)
        {
            float y = offset.Y - offset.Z;

            return new Vector2(offset.X - (int)offset.X, y - (int)y);
        }

        private Matrix ApplySubPixelCamera(Matrix matrix, float zoomScale, bool xbrWorld)
        {
            Profile profile = ProfileManager.CurrentProfile;

            // the camera has no profile access: hand it the zoom option every frame
            Camera.SmoothZoom = profile.SmoothZoom;

            // the xBR filter draws the world at 1:1 art scale: a fraction of a pixel is lost there
            if (!profile.SubPixelCamera || xbrWorld || zoomScale < SUB_PIXEL_MIN_SCALE)
            {
                SubPixelCameraActive = false;

                return matrix;
            }

            // stays on while the player stands still: other mobiles still move smoothly
            SubPixelCameraActive = true;

            return Matrix.CreateTranslation(-_cameraSubPixel.X, -_cameraSubPixel.Y, 0f) * matrix;
        }
    }
}
