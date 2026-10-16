// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;

namespace ClassicUO.Game.Scenes
{
    // Soft shadows hook of the game scene (docs/SHADOWS_PLAN.md, S1)
    internal partial class GameScene
    {
        // Starts collecting this frame's shadows for the soft shadow pass. False when shadows draw
        // the original way (soft shadows off, or shadows off altogether).
        private bool BeginSoftShadows(UltimaBatcher2D batcher)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (!profile.ShadowsEnabled || !profile.SoftShadows)
            {
                return false;
            }

            batcher.BeginDeferredShadows(ShadowSettings.GetStrength(_world.Light.IsometricLevel));

            return true;
        }
    }
}
