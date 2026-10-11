using System;
using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer.Animations;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Renderer
{
    public class XbrSpriteTests
    {
        private const uint T = 0x00000000; // transparent, as in UO animation frames
        private const uint K = 0xFF101010; // dark outline (same RGB as transparent black would be)
        private const uint G = 0xFF808080; // exact gray: dyeable in partial hue mode
        private const uint R = 0xFF2020C0; // a color (ABGR)

        [Fact]
        public void Output_Is_Twice_The_Size_And_Flat_Areas_Stay_Flat()
        {
            uint[] src = Fill(5, 3, R);
            uint[] dst = XbrSprite.Magnify(src, 5, 3);

            Assert.Equal(10 * 6, dst.Length);
            Assert.All(dst, p => Assert.Equal(R, p));
        }

        [Fact]
        public void Never_Invents_Colors()
        {
            // keeps partial hue (exact R == G == B gray detection) working on dyed clothes
            var rnd = new Random(1234);

            for (int n = 0; n < 50; n++)
            {
                int w = rnd.Next(1, 40), h = rnd.Next(1, 40);
                uint[] src = new uint[w * h];

                for (int i = 0; i < src.Length; i++)
                {
                    // noisy shades like UO's pre-rendered frames, with transparent holes
                    src[i] = rnd.Next(5) == 0 ? T : 0xFF000000u | (uint)rnd.Next(0x1000000);
                }

                var input = new HashSet<uint>(src);
                uint[] dst = XbrSprite.Magnify(src, w, h);

                Assert.All(dst, p => Assert.Contains(p, input));
            }
        }

        [Fact]
        public void Each_Output_Pixel_Comes_From_Its_Own_Neighbourhood()
        {
            // nothing travels further than one source pixel: the sprite cannot grow or shift
            var rnd = new Random(7);
            const int w = 30, h = 30;
            uint[] src = new uint[w * h];

            for (int i = 0; i < src.Length; i++)
            {
                src[i] = rnd.Next(3) == 0 ? T : 0xFF000000u | (uint)rnd.Next(0x1000000);
            }

            uint[] dst = XbrSprite.Magnify(src, w, h);

            for (int y = 0; y < h * 2; y++)
            {
                for (int x = 0; x < w * 2; x++)
                {
                    uint p = dst[y * w * 2 + x];
                    int sx = x / 2, sy = y / 2;
                    bool found = false;

                    for (int dy = -1; dy <= 1 && !found; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !found; dx++)
                        {
                            int nx = Math.Clamp(sx + dx, 0, w - 1), ny = Math.Clamp(sy + dy, 0, h - 1);
                            found = src[ny * w + nx] == p;
                        }
                    }

                    Assert.True(found, $"pixel {x},{y} not from its neighbourhood");
                }
            }
        }

        [Fact]
        public void Transparent_Is_Not_Confused_With_Black_Outline()
        {
            // a dark outline next to transparency must keep its shape, not bleed into the background
            const int n = 6;
            uint[] src = Fill(n, n, T);

            for (int x = 0; x < n; x++)
            {
                src[3 * n + x] = K;
            }

            uint[] dst = XbrSprite.Magnify(src, n, n);

            for (int y = 0; y < n * 2; y++)
            {
                for (int x = 0; x < n * 2; x++)
                {
                    bool inLine = y == 6 || y == 7;
                    Assert.Equal(inLine ? K : T, dst[y * n * 2 + x]);
                }
            }
        }

        [Fact]
        public void Diagonal_Edges_Are_Smoothed()
        {
            const int n = 10;
            uint[] src = Fill(n, n, T);

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x <= y; x++)
                {
                    src[y * n + x] = R; // lower-left triangle: a staircase edge
                }
            }

            uint[] dst = XbrSprite.Magnify(src, n, n);

            Assert.NotEqual(Nearest(src, n, n), dst);
        }

        [Fact]
        public void Gray_Pixels_Stay_Exactly_Gray()
        {
            uint[] src = { G, G, R, G, R, R, T, G, R };
            uint[] dst = XbrSprite.Magnify(src, 3, 3);

            Assert.All(dst, p => Assert.True(p == G || p == R || p == T));
        }

        [Fact]
        public void Hd_Slice_Mapping_Doubles_Offsets_And_Size()
        {
            var frameUv = new Rectangle(100, 200, 40, 60);
            var hdUv = new Rectangle(8, 16, 80, 120);

            // a lower slice of the frame, as drawn for characters on mounts
            Rectangle slice = HdCharacterSprites.MapToHd(frameUv, hdUv, new Rectangle(100, 230, 40, 20));

            Assert.Equal(new Rectangle(8, 16 + 60, 80, 40), slice);
            Assert.Equal(hdUv, HdCharacterSprites.MapToHd(frameUv, hdUv, frameUv));
        }

        private static uint[] Fill(int w, int h, uint color)
        {
            uint[] a = new uint[w * h];
            Array.Fill(a, color);

            return a;
        }

        private static uint[] Nearest(uint[] src, int w, int h)
        {
            uint[] dst = new uint[w * h * 4];

            for (int y = 0; y < h * 2; y++)
            {
                for (int x = 0; x < w * 2; x++)
                {
                    dst[y * w * 2 + x] = src[(y / 2) * w + x / 2];
                }
            }

            return dst;
        }
    }
}
