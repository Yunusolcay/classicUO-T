// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Diagnostics;
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
        // enhanced lighting draws this frame (set before the world is drawn)
        private bool _enhancedLightingActive;

        // personal light (0x4E, e.g. night sight) halo around the player, on top of the ambient
        private const float PERSONAL_HALO_STRENGTH = 0.45f;
        private const int PERSONAL_HALO_DIAMETER = 360;
        // body center above the tile anchor of the player sprite
        private const int PERSONAL_HALO_RAISE = 20;

        // Enhanced lighting replaces the normal night darkness only: daylight, alternative lights and
        // the dead black and white view keep the original path.
        private bool UseEnhancedLighting(GraphicsDevice device, RenderTargets renderTargets)
        {
            bool enhanced = CanUseEnhancedLighting(device, renderTargets);
            _enhancedLightingActive = enhanced;

            if (!enhanced)
            {
                // the ambient fade only runs while the enhanced path draws
                _ambientLight.Reset();
                LightingDebug.RecordLegacy();
                EffectLights.Clear();
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

        // The ambient already lights the whole screen to at least the personal level, as before; the
        // halo only adds a soft brighter area around the player, so nobody sees less than before.
        private PersonalHalo GetPersonalHalo()
        {
            int personal = _world.Light.Personal;

            if (personal <= 0 || _world.Player == null)
            {
                return default;
            }

            Vector2 position = _world.Player.GetScreenPosition();

            return new PersonalHalo(
                (int) position.X + 22,
                (int) position.Y + 22 - PERSONAL_HALO_RAISE,
                PERSONAL_HALO_DIAMETER,
                PERSONAL_HALO_STRENGTH * Math.Min(personal, 0x1E) / 0x1E
            );
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
            SetWaterTint(ambient);

            // storm lightning lights up the night as well (docs/WATER_WEATHER_PLAN.md, H3)
            ambient = Vector3.Lerp(ambient, Vector3.One, WeatherRenderer.Flash * 0.7f);

            EffectLights.AddBursts(_offset);

            LightingQualityPreset preset = LightingSettings.GetQualityPreset(profile.LightingQuality);

            long start = LightingDebug.Enabled ? Stopwatch.GetTimestamp() : 0;

            _lightMapRenderer.Draw(batcher, _lightManager, source, destination, worldMatrix, ambient, preset.LightMapScale, preset.BlurPasses, profile.LightIntensity,
                profile.LightFlicker, preset.UseProceduralFalloff, GetPersonalHalo());

            if (LightingDebug.Enabled)
            {
                LightingDebug.RecordEnhanced(
                    _lightMapRenderer.LastLightCount,
                    _lightMapRenderer.LastMaxLights,
                    (int) Math.Ceiling(source.Width * preset.LightMapScale),
                    (int) Math.Ceiling(source.Height * preset.LightMapScale),
                    Stopwatch.GetElapsedTime(start).TotalMilliseconds
                );
            }
        }
    }
}
