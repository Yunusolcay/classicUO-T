using System;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using Xunit;

namespace ClassicUO.UnitTests.Game.Nature
{
    public class NatureTilesTests
    {
        [Theory]
        [InlineData("grass", 1)]
        [InlineData("grasses", 1)]
        [InlineData("pampas grass", 1)]
        [InlineData("orfluer flowers", 1)]
        [InlineData("cattails", 1)]
        [InlineData("tree", 2)]
        [InlineData("oak leaves", 2)]
        [InlineData("plum_tree_trunk", 2)]
        // substring traps
        [InlineData("divine fury", 0)]
        [InlineData("book of bushido", 0)]
        [InlineData("abyssal infernal", 0)]
        [InlineData("eggplant", 0)]
        // attached to walls / ships / furniture / items
        [InlineData("britanianship sails", 0)]
        [InlineData("gargoyle tower vines", 0)]
        [InlineData("flowerpot", 0)]
        [InlineData("grass mat", 0)]
        [InlineData("mosaic_world_tree_5x", 0)]
        [InlineData("tree _hair02_blk_mu", 0)]
        [InlineData("small xmas tree fram", 0)]
        [InlineData("potted tree", 0)]
        [InlineData("wheat shea%ves/f%", 0)]
        [InlineData("", 0)]
        public void Classifies_By_Whole_Words(string name, int expected)
        {
            var data = new StaticTiles(0, 0, 0, 0, 0, 0, 0, 10, name);

            Assert.Equal(expected, NatureTiles.Classify(0, ref data));
        }

        [Fact]
        public void Animated_Wet_Or_Internal_Never_Sway()
        {
            var animated = new StaticTiles((ulong)TileFlag.Animation, 0, 0, 0, 0, 0, 0, 10, "grass");
            var wet = new StaticTiles((ulong)TileFlag.Wet, 0, 0, 0, 0, 0, 0, 10, "reeds");

            Assert.Equal(0, NatureTiles.Classify(0, ref animated));
            Assert.Equal(0, NatureTiles.Classify(0, ref wet));
        }

        [Fact]
        public void Tufts_Stay_Inside_Their_Own_Tile()
        {
            // the 9x12 sprite (bottom-center at center + (dx, dy)) must stay inside the 44x44
            // diamond, so it can only ever overlap its own tile's ground
            for (int x = 0; x < 300; x++)
            {
                for (int y = 0; y < 300; y++)
                {
                    int count = NatureTiles.GetTuftCount(x, y);
                    Assert.InRange(count, 0, 3);

                    for (int i = 0; i < count; i++)
                    {
                        NatureTiles.GetTuft(x, y, i, out int dx, out int dy, out int variant);

                        float half = NatureTiles.TUFT_WIDTH / 2f;
                        float top = dy - NatureTiles.TUFT_HEIGHT;

                        foreach (float cx in new[] { dx - half, dx + half })
                        {
                            Assert.True(Math.Abs(cx) + Math.Abs((float)dy) <= 22, $"bottom corner outside at {x},{y}");
                            Assert.True(Math.Abs(cx) + Math.Abs(top) <= 22, $"top corner outside at {x},{y}");
                        }

                        Assert.InRange(variant, 0, 7);
                    }
                }
            }
        }

        [Fact]
        public void Tufts_Are_Stable_And_Varied()
        {
            NatureTiles.GetTuft(1234, 567, 1, out int dx1, out int dy1, out int v1);
            NatureTiles.GetTuft(1234, 567, 1, out int dx2, out int dy2, out int v2);
            Assert.Equal((dx1, dy1, v1), (dx2, dy2, v2));

            int[] histogram = new int[4];

            for (int x = 0; x < 100; x++)
            {
                for (int y = 0; y < 100; y++)
                {
                    histogram[NatureTiles.GetTuftCount(x, y)]++;
                }
            }

            // roughly 25 / 35 / 30 / 10 percent
            Assert.InRange(histogram[0], 2000, 3000);
            Assert.InRange(histogram[3], 600, 1400);
        }

        [Fact]
        public void Tint_Is_Smooth_Between_Neighbours()
        {
            for (int x = 0; x < 200; x++)
            {
                for (int y = 0; y < 200; y++)
                {
                    float t = NatureTiles.GetTint(x, y);

                    Assert.InRange(t, -1f, 1f);
                    Assert.True(Math.Abs(t - NatureTiles.GetTint(x + 1, y)) < 0.25f);
                }
            }
        }
    }
}
