// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes
{
    // Water shader parameters of the game scene (docs/WATER_WEATHER_PLAN.md, W1/W2)
    internal partial class GameScene
    {
        // last enhanced lighting ambient color; the world is drawn before this frame's lights
        private Vector3 _waterTint = Vector3.One;

        private void SetWaterParameters(UltimaBatcher2D batcher)
        {
            // vertex positions are relative to the draw offset: this makes the water pattern absolute
            batcher.SetWorldOrigin(new Vector2(_offset.X, _offset.Y));
            DrawOffset = _offset;
            batcher.SetAmbientTint(_enhancedLightingActive ? _waterTint : Vector3.One);
        }

        // The ambient color with its brightness taken out: the darkness itself comes from the light
        // composite, the water only takes the hue (moonlight blue at night)
        private void SetWaterTint(Vector3 ambient)
        {
            float max = MathHelper.Max(ambient.X, MathHelper.Max(ambient.Y, ambient.Z));

            _waterTint = max > 0.001f ? ambient / max : Vector3.One;
        }
    }
}
