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

        // read by MobileView while drawing the player
        public static bool SubPixelCameraActive { get; private set; }
        public static Vector2 PlayerSubPixelOffset { get; private set; }

        private Matrix ApplySubPixelCamera(Matrix matrix, float zoomScale, bool xbrWorld)
        {
            Profile profile = ProfileManager.CurrentProfile;

            // the camera has no profile access: hand it the zoom option every frame
            Camera.SmoothZoom = profile.SmoothZoom;

            // the xBR filter draws the world at 1:1 art scale: a fraction of a pixel is lost there
            if (!profile.SubPixelCamera || xbrWorld || zoomScale < SUB_PIXEL_MIN_SCALE || _cameraSubPixel == Vector2.Zero)
            {
                SubPixelCameraActive = false;
                PlayerSubPixelOffset = Vector2.Zero;

                return matrix;
            }

            SubPixelCameraActive = true;
            PlayerSubPixelOffset = _cameraSubPixel;

            return Matrix.CreateTranslation(-_cameraSubPixel.X, -_cameraSubPixel.Y, 0f) * matrix;
        }
    }
}
