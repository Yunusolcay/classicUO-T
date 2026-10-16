// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Lighting;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class LightingSettingsTests
    {
        [Theory]
        [InlineData(0, 0.25f, 0, 0, 128, false)]
        [InlineData(1, 0.5f, 0, 0, 256, true)]
        [InlineData(2, 0.5f, 2, 9, 512, true)]
        [InlineData(99, 0.5f, 0, 0, 256, true)]
        public void QualityPresets_MatchLightingPlan_AndUnknownQualityUsesMedium(
            int quality, float lightMapScale, int blurPasses, int blurTaps,
            int maxLights, bool useProceduralFalloff)
        {
            LightingQualityPreset preset = LightingSettings.GetQualityPreset((LightingQuality)quality);

            Assert.Equal(lightMapScale, preset.LightMapScale);
            Assert.Equal(blurPasses, preset.BlurPasses);
            Assert.Equal(blurTaps, preset.BlurTaps);
            Assert.Equal(maxLights, preset.MaxLights);
            Assert.Equal(useProceduralFalloff, preset.UseProceduralFalloff);
        }
    }
}
