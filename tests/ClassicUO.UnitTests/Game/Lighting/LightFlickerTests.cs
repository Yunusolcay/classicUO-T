// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Lighting;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class LightFlickerTests
    {
        [Fact]
        public void None_LeavesTheLightUnchanged()
        {
            LightFlicker.Sample(LightFlickerType.None, 12345, 7, out float intensity, out float scale);

            Assert.Equal(1f, intensity);
            Assert.Equal(1f, scale);
        }

        [Theory]
        [InlineData((int)LightFlickerType.Fire, 0.8f)]
        [InlineData((int)LightFlickerType.Magic, 0.85f)]
        public void Flicker_StaysInItsRange_AndActuallyMoves(int typeValue, float minIntensity)
        {
            LightFlickerType type = (LightFlickerType)typeValue;
            float min = float.MaxValue, max = float.MinValue;

            for (uint ticks = 0; ticks < 10_000; ticks += 16)
            {
                LightFlicker.Sample(type, ticks, 42, out float intensity, out float scale);

                Assert.InRange(intensity, minIntensity, 1f);
                Assert.InRange(scale, 0.95f, 1.05f);

                min = System.Math.Min(min, intensity);
                max = System.Math.Max(max, intensity);
            }

            Assert.True(max - min > 0.05f);
        }

        [Fact]
        public void Flicker_DependsOnTimeOnly_NotOnHowOftenItIsSampled()
        {
            LightFlicker.Sample(LightFlickerType.Fire, 5000, 3, out float a, out _);
            LightFlicker.Sample(LightFlickerType.Fire, 1234, 3, out _, out _);
            LightFlicker.Sample(LightFlickerType.Fire, 5000, 3, out float b, out _);

            Assert.Equal(a, b);
        }

        [Fact]
        public void DifferentSeeds_FlickerOutOfStep()
        {
            LightFlicker.Sample(LightFlickerType.Fire, 5000, 1, out float a, out _);
            LightFlicker.Sample(LightFlickerType.Fire, 5000, 2, out float b, out _);

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void LongSessions_StillFlickerSmoothly()
        {
            // about 40 days of Time.Ticks: neighbouring frames must stay close
            const uint late = 3_500_000_000;

            LightFlicker.Sample(LightFlickerType.Fire, late, 9, out float a, out _);
            LightFlicker.Sample(LightFlickerType.Fire, late + 16, 9, out float b, out _);

            Assert.InRange(System.Math.Abs(a - b), 0f, 0.05f);
        }
    }
}
