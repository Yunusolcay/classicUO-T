// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Scenes
{
    // Soft shadows hooks of the game scene (docs/SHADOWS_PLAN.md)
    internal partial class GameScene
    {
        private const int MAX_SHADOW_LIGHTS = 64;

        private readonly ShadowLight[] _shadowLights = new ShadowLight[MAX_SHADOW_LIGHTS];
        private float _sunShadowStrength;

        // Starts collecting this frame's shadows for the soft shadow pass. False when shadows draw
        // the original way (soft shadows off, or shadows off altogether).
        private bool BeginSoftShadows(UltimaBatcher2D batcher)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (!profile.ShadowsEnabled || !profile.SoftShadows)
            {
                LightingDebug.RecordShadows(-1);

                return false;
            }

            _sunShadowStrength = ShadowSettings.GetStrength(_world.Light.IsometricLevel);
            batcher.BeginDeferredShadows(_sunShadowStrength, ShadowSettings.CONTACT_STRENGTH);

            return true;
        }

        // Draws the collected shadows; at night the enhanced lights collected this frame cast them too
        private void DrawSoftShadows(UltimaBatcher2D batcher, RenderTarget2D target)
        {
            int lightCount = 0;
            float localStrength = 0f;

            if (_enhancedLightingActive)
            {
                localStrength = ShadowSettings.GetLocalStrength(_sunShadowStrength);

                if (localStrength > 0f)
                {
                    lightCount = _lightManager.GetShadowLights(_shadowLights);
                }
            }

            batcher.DrawDeferredShadows(
                target.Width,
                target.Height,
                new ReadOnlySpan<ShadowLight>(_shadowLights, 0, lightCount),
                localStrength
            );

            LightingDebug.RecordShadows(batcher.LastShadowCount);
        }
    }
}
