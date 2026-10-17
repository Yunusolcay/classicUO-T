// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Assets;
using ClassicUO.Game.Lighting;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class ShadowSettingsTests
    {
        [Fact]
        public void Daylight_KeepsTheOriginalShadowDarkness()
        {
            Assert.Equal(0.4f, ShadowSettings.GetStrength(1f));
            Assert.Equal(0.4f, ShadowSettings.GetStrength(ShadowSettings.FADE_START));
        }

        [Fact]
        public void Night_HasNoSunShadows()
        {
            Assert.Equal(0f, ShadowSettings.GetStrength(ShadowSettings.FADE_END));
            Assert.Equal(0f, ShadowSettings.GetStrength(0.1f));
        }

        [Fact]
        public void Dusk_FadesSmoothlyAndMonotonically()
        {
            float previous = 0f;

            for (float level = ShadowSettings.FADE_END; level <= ShadowSettings.FADE_START; level += 0.01f)
            {
                float strength = ShadowSettings.GetStrength(level);

                Assert.InRange(strength, previous, ShadowSettings.DAY_STRENGTH);
                previous = strength;
            }

            float middle = ShadowSettings.GetStrength((ShadowSettings.FADE_START + ShadowSettings.FADE_END) * 0.5f);
            Assert.Equal(ShadowSettings.DAY_STRENGTH * 0.5f, middle, 4);
        }

        [Fact]
        public void LightShadows_TakeOverAsTheSunFades()
        {
            Assert.Equal(0f, ShadowSettings.GetLocalStrength(ShadowSettings.DAY_STRENGTH));
            Assert.Equal(ShadowSettings.LOCAL_STRENGTH, ShadowSettings.GetLocalStrength(0f));
            Assert.Equal(ShadowSettings.LOCAL_STRENGTH * 0.5f, ShadowSettings.GetLocalStrength(ShadowSettings.DAY_STRENGTH * 0.5f), 4);
        }

        private static StaticTiles Tile(TileFlag flags, byte height) => new StaticTiles((ulong)flags, 0, 0, 0, 0, 0, 0, height, "tile");

        [Fact]
        public void Occlusion_WallsAndSolidObjects()
        {
            StaticTiles wall = Tile(TileFlag.Wall | TileFlag.Impassable, 20);
            StaticTiles crate = Tile(TileFlag.Impassable, 6);

            Assert.True(ShadowSettings.IsOccluder(ref wall));
            Assert.True(ShadowSettings.IsOccluder(ref crate));
        }

        [Fact]
        public void Occlusion_NotForFlatRoofWaterFoliageOrDoors()
        {
            StaticTiles flat = Tile(TileFlag.Impassable, 1);
            StaticTiles roof = Tile(TileFlag.Roof | TileFlag.Impassable, 20);
            StaticTiles water = Tile(TileFlag.Wet | TileFlag.Impassable, 10);
            StaticTiles leaves = Tile(TileFlag.Foliage | TileFlag.Impassable, 20);
            StaticTiles door = Tile(TileFlag.Door | TileFlag.Impassable, 20);
            StaticTiles floor = Tile(TileFlag.Surface, 0);

            Assert.False(ShadowSettings.IsOccluder(ref flat));
            Assert.False(ShadowSettings.IsOccluder(ref roof));
            Assert.False(ShadowSettings.IsOccluder(ref water));
            Assert.False(ShadowSettings.IsOccluder(ref leaves));
            Assert.False(ShadowSettings.IsOccluder(ref door));
            Assert.False(ShadowSettings.IsOccluder(ref floor));
        }
    }
}
