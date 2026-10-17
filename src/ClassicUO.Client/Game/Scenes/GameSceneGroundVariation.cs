// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Renderer;

namespace ClassicUO.Game.Scenes
{
    // Ground variation hooks of the game scene (docs/GROUND_VARIATION_PLAN.md)
    internal partial class GameScene
    {
        private static void BeginGroundVariation(UltimaBatcher2D batcher)
        {
            batcher.SetGroundVariation(ProfileManager.CurrentProfile.GroundVariation ? 1f : 0f);
        }

        private static void EndGroundVariation(UltimaBatcher2D batcher)
        {
            batcher.SetGroundVariation(0f);
        }
    }
}
