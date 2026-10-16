// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Lighting;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class EffectLightsTests
    {
        // 0xAABBGGRR, like the art loader
        private static uint Pixel(byte r, byte g, byte b) => 0xFF000000u | (uint)(b << 16) | (uint)(g << 8) | r;

        [Fact]
        public void Average_TakesTheColorAndDropsTheBrightness()
        {
            uint[] pixels = { Pixel(200, 100, 0), Pixel(200, 100, 0), 0, 0 };

            Vector3 color = EffectLights.Average(pixels);

            Assert.Equal(1f, color.X, 3);
            Assert.Equal(0.5f, color.Y, 3);
            Assert.Equal(0f, color.Z, 3);
        }

        [Fact]
        public void Average_IgnoresTransparentPixels_AndDarkArt()
        {
            Assert.Equal(Vector3.Zero, EffectLights.Average(new uint[] { 0, 0x00FFFFFF }));
            Assert.Equal(Vector3.Zero, EffectLights.Average(new[] { Pixel(20, 20, 30) }));
        }

        [Fact]
        public void Lights_AreCollectedUpToTheLimit_AndCleared()
        {
            EffectLights.Clear();

            for (int i = 0; i < EffectLights.MAX_LIGHTS + 10; i++)
            {
                EffectLights.Add(Vector2.Zero, 50f, Vector3.One);
            }

            Assert.Equal(EffectLights.MAX_LIGHTS, EffectLights.Count);

            EffectLights.Clear();
            Assert.Equal(0, EffectLights.Count);
        }
    }
}
