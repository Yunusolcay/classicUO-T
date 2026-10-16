// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Lighting;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class LightingDebugTests
    {
        [Fact]
        public void Text_LabelsThePassTimeAsCpu_AndShowsTheCounts()
        {
            LightingDebug.RecordLegacy();
            LightingDebug.RecordEnhanced(42, 256, 640, 360, 0.5);

            string text = LightingDebug.GetText();

            Assert.Contains("42/256 lights", text);
            Assert.Contains("640x360", text);
            Assert.Contains("CPU 0.50 ms (not GPU time)", text);
        }

        [Fact]
        public void Text_SaysWhenTheOriginalPathDraws()
        {
            LightingDebug.RecordLegacy();

            Assert.Contains("original path", LightingDebug.GetText());
        }

        [Fact]
        public void Text_ShowsTheSoftShadowCount()
        {
            LightingDebug.RecordShadows(17);
            Assert.Contains("17 soft shadows", LightingDebug.GetText());

            LightingDebug.RecordShadows(-1);
            Assert.Contains("Shadows: original", LightingDebug.GetText());
        }
    }
}
