// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes
{
    // Surface lighting hooks of the game scene (docs/SURFACE_LIGHTING_PLAN.md)
    internal partial class GameScene
    {
        // towards the sun in screen space (x right, y down, z towards the viewer): high in the upper
        // left, like the light painted into the UO art
        private static readonly Vector3 SUN_DIRECTION = new Vector3(-0.55f, -0.65f, 0.6f);
        // strength in full daylight; at night the relief fades to NIGHT_SHARE of it
        private const float SURFACE_LIGHT_STRENGTH = 0.6f;
        private const float NIGHT_SHARE = 0.35f;

        private void BeginSurfaceLight(UltimaBatcher2D batcher)
        {
            Profile profile = ProfileManager.CurrentProfile;
            float strength = 0f;

            if (profile.SurfaceLighting && HdWorldPack.Active)
            {
                float daylight = MathHelper.Clamp(_world.Light.IsometricLevel, 0f, 1f);
                strength = SURFACE_LIGHT_STRENGTH * MathHelper.Lerp(NIGHT_SHARE, 1f, daylight);
            }

            batcher.SetSurfaceLight(SUN_DIRECTION, strength);
        }

        private static void EndSurfaceLight(UltimaBatcher2D batcher)
        {
            batcher.SetSurfaceLight(SUN_DIRECTION, 0f);
        }
    }
}
