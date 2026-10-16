// SPDX-License-Identifier: BSD-2-Clause

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
    }
}
