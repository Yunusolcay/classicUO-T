// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ClassicUO.Game.Lighting;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class LightDefinitionsTests
    {
        [Fact]
        public void Parse_ReadsAllColumns_HexAndDecimalIds()
        {
            Dictionary<ushort, LightDefinition> result = LightDefinitions.Parse(
                "0x0A12 1.2 0.8 fire round\n2575 0.5 1 magic texture\n",
                null
            );

            LightDefinition torch = result[0x0A12];
            Assert.Equal(1.2f, torch.Radius);
            Assert.Equal(0.8f, torch.Intensity);
            Assert.Equal(LightFlickerType.Fire, torch.Flicker);
            Assert.Equal(LightShape.Round, torch.Shape);

            LightDefinition other = result[2575];
            Assert.Equal(LightFlickerType.Magic, other.Flicker);
            Assert.Equal(LightShape.Texture, other.Shape);
        }

        [Fact]
        public void Parse_ShapeIsOptional_AndDefaultsToTexture()
        {
            Dictionary<ushort, LightDefinition> result = LightDefinitions.Parse("0x0A07 1 1 none", null);

            Assert.Equal(LightShape.Texture, result[0x0A07].Shape);
            Assert.Equal(LightFlickerType.None, result[0x0A07].Flicker);
        }

        [Fact]
        public void Parse_ExpandsRanges()
        {
            Dictionary<ushort, LightDefinition> result = LightDefinitions.Parse("0x0DE3-0x0DE8 1.3 1 fire round", null);

            Assert.Equal(6, result.Count);
            Assert.True(result.ContainsKey(0x0DE3));
            Assert.True(result.ContainsKey(0x0DE8));
        }

        [Theory]
        [InlineData("0x0A12 1.2 0.8")]
        [InlineData("zz 1 1 fire")]
        [InlineData("0x0A12 big 1 fire")]
        [InlineData("0x0A12 1 1 smoke")]
        [InlineData("0x0A12 1 1 fire square")]
        [InlineData("0x0A12 1 1 7")]
        [InlineData("0x0A14-0x0A12 1 1 fire")]
        public void Parse_SkipsAndReportsBadLines(string line)
        {
            List<string> warnings = new List<string>();

            Dictionary<ushort, LightDefinition> result = LightDefinitions.Parse(line + "\n0x0A0F 1 1 fire", warnings.Add);

            Assert.Single(result);
            Assert.True(result.ContainsKey(0x0A0F));
            Assert.Single(warnings);
        }

        [Fact]
        public void Parse_ClampsRadiusAndIntensity()
        {
            Dictionary<ushort, LightDefinition> result = LightDefinitions.Parse("1 99 5 none\n2 0 -1 none", null);

            Assert.Equal(LightDefinitions.MAX_RADIUS, result[1].Radius);
            Assert.Equal(1f, result[1].Intensity);
            Assert.Equal(0.1f, result[2].Radius);
            Assert.Equal(0f, result[2].Intensity);
        }

        [Fact]
        public void Parse_UsesInvariantDecimalPoint_OnTurkishCulture()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");

                Dictionary<ushort, LightDefinition> result = LightDefinitions.Parse("0x0A12 1.5 0.5 FIRE ROUND", null);

                Assert.Equal(1.5f, result[0x0A12].Radius);
                Assert.Equal(LightShape.Round, result[0x0A12].Shape);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void DefaultFile_ParsesCleanly_AndCoversTheCommonLights()
        {
            List<string> warnings = new List<string>();

            Dictionary<ushort, LightDefinition> result = LightDefinitions.Parse(LightDefinitions.DEFAULT_FILE, warnings.Add);

            Assert.Empty(warnings);
            Assert.True(result.Count >= 25);

            // candle, torch, wall torch, lantern, brazier, camp fire, fire field, energy field, moongate
            foreach (ushort id in new ushort[] { 0x0A0F, 0x0A12, 0x0A07, 0x0A15, 0x0E31, 0x0DE3, 0x398C, 0x3946, 0x0F6C })
            {
                Assert.True(result.ContainsKey(id), $"missing 0x{id:X4}");
            }

            // wall mounted lights keep their one sided light.mul shape
            Assert.Equal(LightShape.Texture, result[0x0A07].Shape);
        }
    }
}
