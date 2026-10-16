// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Assets;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class LightManagerTests
    {
        [Fact]
        public void DefaultLimit_PreservesLegacyFirstHundredLights()
        {
            var manager = new LightManager(null);

            Assert.Equal(100, manager.MaxLights);
            AssertAcceptsExactly(manager, 100);
        }

        [Theory]
        [InlineData(0, 128)]
        [InlineData(1, 256)]
        [InlineData(2, 512)]
        public void QualityLimit_CollectsMoreLightsThanThereAreAssetIds(int quality, int expectedLimit)
        {
            var manager = new LightManager(null)
            {
                MaxLights = LightingSettings.GetQualityPreset((LightingQuality)quality).MaxLights
            };

            AssertAcceptsExactly(manager, expectedLimit);
        }

        [Fact]
        public void InvalidAssetId_IsRejectedWithoutConsumingASlot()
        {
            var manager = new LightManager(null) { MaxLights = 512 };

            Assert.False(manager.TryAdd(new LightSource { ID = LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT }));
            Assert.Equal(0, manager.Count);
            Assert.True(manager.TryAdd(new LightSource { ID = LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT - 1 }));
            Assert.Equal(1, manager.Count);
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, 0)]
        [InlineData(513, 512)]
        [InlineData(int.MaxValue, 512)]
        public void Limit_IsBoundedByCollectionCapacity(int requestedLimit, int expectedLimit)
        {
            var manager = new LightManager(null) { MaxLights = requestedLimit };

            Assert.Equal(expectedLimit, manager.MaxLights);
            AssertAcceptsExactly(manager, expectedLimit);
        }

        [Fact]
        public void RaisingLimit_AllowsCollectionToContinueAfterLegacyLimit()
        {
            var manager = new LightManager(null);
            AssertAcceptsExactly(manager, 100);

            manager.MaxLights = LightingSettings.GetQualityPreset(LightingQuality.Low).MaxLights;

            for (int i = 100; i < 128; i++)
            {
                Assert.True(manager.TryAdd(new LightSource { ID = 1 }));
            }

            Assert.False(manager.TryAdd(new LightSource { ID = 1 }));
            Assert.Equal(128, manager.Count);
        }

        [Fact]
        public void Add_WhenFull_StopsBeforeResolvingWorldOrAssetData()
        {
            var manager = new LightManager(null) { MaxLights = 1 };
            Assert.True(manager.TryAdd(new LightSource { ID = 1 }));

            // The world and map are deliberately absent: a full manager must not access them.
            var lightObject = new TestGameObject();
            manager.Add(lightObject, lightObject, 0, 0, 0);

            Assert.Equal(1, manager.Count);
        }

        private static void AssertAcceptsExactly(LightManager manager, int expectedLimit)
        {
            for (int i = 0; i < expectedLimit; i++)
            {
                Assert.True(manager.TryAdd(new LightSource
                {
                    ID = (byte)(i % LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT),
                    DrawX = i,
                    DrawY = i + 1
                }));
            }

            for (int i = 0; i < 32; i++)
            {
                Assert.False(manager.TryAdd(new LightSource { ID = 1 }));
            }

            Assert.Equal(expectedLimit, manager.Count);
        }

        private sealed class TestGameObject : GameObject
        {
            public TestGameObject() : base(null) { }

            public override bool CheckMouseSelection() => false;

            public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth) => false;
        }
    }
}
