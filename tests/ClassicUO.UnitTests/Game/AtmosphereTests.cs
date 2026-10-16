// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game;
using Xunit;

namespace ClassicUO.UnitTests.Game
{
    public class AtmosphereTests
    {
        [Theory]
        [InlineData("snow", "Snow")]
        [InlineData("decimated_snow", "Snow")]
        [InlineData("cave", "Dungeon")]
        [InlineData("cave exit", "Dungeon")]
        [InlineData("lava", "Volcanic")]
        [InlineData("obsidian", "Volcanic")]
        [InlineData("acid", "Swamp")]
        [InlineData("jungle", "Jungle")]
        [InlineData("voidswell", "Void")]
        [InlineData("sand", "Desert")]
        [InlineData("sand stone", "Neutral")]
        [InlineData("grass", "Neutral")]
        [InlineData("", "Neutral")]
        public void LandNames_MapToTheirPlace(string name, string region)
        {
            Assert.Equal(region, AtmosphereRenderer.Classify(name).ToString());
        }
    }
}
