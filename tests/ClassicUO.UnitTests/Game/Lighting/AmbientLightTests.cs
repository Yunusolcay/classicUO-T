// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Lighting;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class AmbientLightTests
    {
        [Fact]
        public void FirstUpdate_StartsAtTheTarget()
        {
            AmbientLight ambient = new AmbientLight();

            Vector3 color = ambient.Update(0.3f, 1000, 2f, false);

            Assert.Equal(new Vector3(0.3f), color);
        }

        [Fact]
        public void LevelChange_FadesWithSmoothStep_AndSettlesOnTheTarget()
        {
            AmbientLight ambient = new AmbientLight();
            ambient.Update(1f, 0, 2f, false);

            ambient.Update(0.2f, 0, 2f, false);
            Assert.Equal(1f, ambient.Level, 4);

            ambient.Update(0.2f, 1000, 2f, false);
            Assert.Equal(0.6f, ambient.Level, 4);

            ambient.Update(0.2f, 500, 2f, false);
            ambient.Update(0.2f, 2000, 2f, false);
            Assert.Equal(0.2f, ambient.Level, 4);
        }

        [Fact]
        public void NewTargetWhileFading_ContinuesFromTheCurrentLevel()
        {
            AmbientLight ambient = new AmbientLight();
            ambient.Update(1f, 0, 2f, false);
            ambient.Update(0f, 0, 2f, false);
            ambient.Update(0f, 1000, 2f, false);

            ambient.Update(1f, 1000, 2f, false);

            Assert.Equal(0.5f, ambient.Level, 4);
        }

        [Fact]
        public void ZeroTransition_And_Reset_JumpStraightToTheTarget()
        {
            AmbientLight ambient = new AmbientLight();
            ambient.Update(1f, 0, 0f, false);
            Assert.Equal(0.25f, ambient.Update(0.25f, 10, 0f, false).X, 4);

            ambient.Update(1f, 20, 2f, false);
            ambient.Reset();
            Assert.Equal(0.4f, ambient.Update(0.4f, 30, 2f, false).X, 4);
        }

        [Fact]
        public void WithoutTint_ColorIsTheOriginalGray()
        {
            Assert.Equal(new Vector3(0.35f), AmbientLight.GetColor(0.35f, false));
        }

        [Fact]
        public void Tint_IsNeutralInDaylight_AndCoolAtNight()
        {
            Assert.Equal(Vector3.One, AmbientLight.GetColor(1f, true));

            Vector3 night = AmbientLight.GetColor(0.2f, true);

            Assert.True(night.Z > night.Y && night.Y > night.X);
            Assert.InRange(night.X, 0f, 1f);
            Assert.InRange(night.Z, 0f, 1f);
        }
    }
}
