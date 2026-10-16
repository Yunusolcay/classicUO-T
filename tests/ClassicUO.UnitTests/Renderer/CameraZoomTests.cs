// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Renderer
{
    public class CameraZoomTests
    {
        private static Camera CreateCamera(bool smooth)
        {
            Camera camera = new Camera(0.5f, 2.5f, 0.1f) { SmoothZoom = smooth };
            camera.Bounds = new Rectangle(0, 0, 800, 600);

            return camera;
        }

        [Fact]
        public void SmoothZoom_AnimatesTowardsTheTarget()
        {
            Camera camera = CreateCamera(true);

            camera.ZoomOut();

            Assert.Equal(1f, camera.Zoom);
            Assert.Equal(1.1f, camera.TargetZoom, 4);

            camera.Update(true, 0.05f, Point.Zero);
            Assert.InRange(camera.Zoom, 1.0001f, 1.0999f);

            camera.Update(true, 1f, Point.Zero);
            Assert.Equal(1.1f, camera.Zoom, 4);
        }

        [Fact]
        public void SmoothZoom_IsFrameRateIndependent()
        {
            Camera slow = CreateCamera(true);
            Camera fast = CreateCamera(true);
            slow.ZoomOut();
            fast.ZoomOut();

            slow.Update(true, 0.04f, Point.Zero);

            for (int i = 0; i < 4; i++)
            {
                fast.Update(true, 0.01f, Point.Zero);
            }

            Assert.Equal(slow.Zoom, fast.Zoom, 4);
        }

        [Fact]
        public void RepeatedSteps_AddUpOnTheTarget()
        {
            Camera camera = CreateCamera(true);

            camera.ZoomOut();
            camera.ZoomOut();
            camera.ZoomOut();

            Assert.Equal(1.3f, camera.TargetZoom, 4);
        }

        [Fact]
        public void SettingZoom_AndSmoothZoomOff_Jump()
        {
            Camera camera = CreateCamera(true);
            camera.ZoomOut();
            camera.Zoom = 2f;

            Assert.Equal(2f, camera.Zoom);
            Assert.Equal(2f, camera.TargetZoom);

            Camera off = CreateCamera(false);
            off.ZoomIn();

            Assert.Equal(0.9f, off.Zoom, 4);
        }

        [Fact]
        public void Target_IsClampedToTheZoomRange()
        {
            Camera camera = CreateCamera(true);

            for (int i = 0; i < 40; i++)
            {
                camera.ZoomOut();
            }

            Assert.Equal(2.5f, camera.TargetZoom);
        }
    }
}
