// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Scenes;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Game.Scenes
{
    public class SubPixelCameraTests
    {
        [Fact]
        public void SubPixel_IsWhatWholePixelDrawingDrops()
        {
            Vector2 fraction = GameScene.GetSubPixel(new Vector3(10.75f, 4.5f, 1.25f));

            // x: 10.75 - 10, y: (4.5 - 1.25) = 3.25 - 3
            Assert.Equal(0.75f, fraction.X, 4);
            Assert.Equal(0.25f, fraction.Y, 4);
        }

        [Fact]
        public void SubPixel_KeepsTheSignOfNegativeSteps()
        {
            // (int) truncates towards zero, like the camera and MobileView
            Vector2 fraction = GameScene.GetSubPixel(new Vector3(-10.75f, -3.5f, 0f));

            Assert.Equal(-0.75f, fraction.X, 4);
            Assert.Equal(-0.5f, fraction.Y, 4);
        }

        [Fact]
        public void SubPixel_IsZeroOnWholePixels()
        {
            Assert.Equal(Vector2.Zero, GameScene.GetSubPixel(new Vector3(22f, 11f, 0f)));
        }
    }
}
