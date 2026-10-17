// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Lighting;
using ClassicUO.Renderer.Lighting;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class SurfaceNormalBuilderTests
    {
        private const uint GRAY = 0xFF808080;
        private const uint WHITE = 0xFFFFFFFF;
        private const uint BLACK = 0xFF000000;

        private static uint[] Fill(int width, int height, uint color)
        {
            var pixels = new uint[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            return pixels;
        }

        private static int NormalX(uint texel) => (int)(texel & 0xFF);
        private static int NormalY(uint texel) => (int)((texel >> 8) & 0xFF);
        private static int Open(uint texel) => (int)(texel >> 24);

        [Fact]
        public void EvenColor_IsFlatAndOpen()
        {
            uint[] normals = SurfaceNormalBuilder.Build(Fill(8, 8, GRAY), 8, 8);

            Assert.All(normals, texel => Assert.Equal(SurfaceNormals.FLAT, texel));
        }

        [Fact]
        public void Transparent_IsFlat()
        {
            uint[] pixels = Fill(6, 6, 0);
            pixels[14] = WHITE;

            uint[] normals = SurfaceNormalBuilder.Build(pixels, 6, 6);

            Assert.Equal(SurfaceNormals.FLAT, normals[0]);
            // the outline does not tilt a lone pixel
            Assert.Equal(SurfaceNormals.FLAT, normals[14]);
        }

        [Fact]
        public void BrighterToTheRight_TiltsLeft()
        {
            var pixels = new uint[8 * 4];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] = x < 4 ? BLACK : WHITE;
                }
            }

            uint[] normals = SurfaceNormalBuilder.Build(pixels, 8, 4);
            uint edge = normals[1 * 8 + 4];

            // rising towards +x: the surface faces -x
            Assert.True(NormalX(edge) < 128);
            Assert.Equal(128, NormalY(edge));
        }

        [Fact]
        public void DarkGap_IsACavity()
        {
            uint[] pixels = Fill(9, 9, WHITE);
            pixels[4 * 9 + 4] = BLACK;

            uint[] normals = SurfaceNormalBuilder.Build(pixels, 9, 9);

            Assert.True(Open(normals[4 * 9 + 4]) < 255);
            Assert.Equal(255, Open(normals[0]));
        }
    }
}
