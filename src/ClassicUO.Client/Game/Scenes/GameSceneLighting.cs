// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Scenes
{
    // Enhanced lighting hooks of the game scene (docs/LIGHTING_PLAN.md, L6)
    internal partial class GameScene
    {
        private static readonly Func<BlendState> _noLightsBlend = () => null;
        private static readonly Func<Vector3> _noLightsHue = () => Vector3.UnitZ;

        private readonly LightMapRenderer _lightMapRenderer = new LightMapRenderer();
        private readonly AmbientLight _ambientLight = new AmbientLight();

        // Enhanced lighting replaces the normal night darkness only: daylight, alternative lights and
        // the dead black and white view keep the original path.
        private bool UseEnhancedLighting(GraphicsDevice device, RenderTargets renderTargets)
        {
            bool enhanced = CanUseEnhancedLighting(device, renderTargets);

            if (!enhanced)
            {
                // the ambient fade only runs while the enhanced path draws
                _ambientLight.Reset();
            }

            return enhanced;
        }

        private bool CanUseEnhancedLighting(GraphicsDevice device, RenderTargets renderTargets)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (
                !profile.EnhancedLighting
                || !UseLights
                || UseAltLights
                || _world.Player.IsDead && profile.EnableBlackWhiteEffect
                || renderTargets.WorldRenderTarget == null
            )
            {
                return false;
            }

            if (!_lightMapRenderer.EnsureResources(device))
            {
                profile.EnhancedLighting = false;

                return false;
            }

            return true;
        }

        // The lights are already in the world image: RenderTargets must not darken it again
        private static void DisableLegacyLightLayer(UltimaBatcher2D batcher, RenderTargets renderTargets)
        {
            renderTargets.SetLightsConfiguration(_noLightsBlend, _noLightsHue);

            batcher.GraphicsDevice.SetRenderTarget(renderTargets.LightRenderTarget);
            batcher.GraphicsDevice.Clear(ClearOptions.Target, Color.Transparent, 0f, 0);
            batcher.GraphicsDevice.SetRenderTarget(null);
        }

        // Writes the world drawn into source, lit by this frame's lights, into destination
        private void DrawEnhancedLights(UltimaBatcher2D batcher, RenderTarget2D source, RenderTarget2D destination, Matrix worldMatrix)
        {
            Profile profile = ProfileManager.CurrentProfile;

            float level = _world.Light.IsometricLevel;

            if (profile.UseDarkNights)
            {
                level -= 0.04f;
            }

            Vector3 ambient = _ambientLight.Update(level, Time.Ticks, profile.LightTransitionSeconds, profile.DayNightTint);

            LightingQualityPreset preset = LightingSettings.GetQualityPreset(profile.LightingQuality);

            _lightMapRenderer.Draw(batcher, _lightManager, source, destination, worldMatrix, ambient, preset.LightMapScale, preset.BlurPasses, profile.LightIntensity,
                profile.LightFlicker, preset.UseProceduralFalloff);
        }
    }
}
