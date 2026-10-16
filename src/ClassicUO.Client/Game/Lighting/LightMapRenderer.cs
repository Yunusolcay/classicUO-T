// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Renderer;
using ClassicUO.Renderer.Lighting;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Lighting
{
    // Enhanced lighting (docs/LIGHTING_PLAN.md): the frame's lights go into a scaled light map that
    // stores half the light, then the world image is written out multiplied by twice the light map.
    // 0.5 in the light map leaves the scene unchanged, above it lights brighten past the art up to a
    // soft limit (LightingEffect.Composite).
    internal sealed class LightMapRenderer : IDisposable
    {
        // largest brightening above the art at light intensity 1
        private const float OVERBRIGHT_LIMIT = 0.35f;
        // the light colors keep this much of their saturation
        private const float LIGHT_SATURATION = 0.75f;

        // additive at half strength: the light map encoding
        private static readonly BlendState _halfAdditive = new BlendState
        {
            ColorSourceBlend = Blend.BlendFactor,
            ColorDestinationBlend = Blend.One,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            BlendFactor = new Color(128, 128, 128, 128)
        };

        private readonly LightMapTarget _targets = new LightMapTarget();
        private LightingEffect _effect;
        private bool _failed;

        // Loads the lighting shader once. False when the graphics backend cannot load it: the
        // caller then keeps the original lighting.
        public bool EnsureResources(GraphicsDevice device)
        {
            if (_effect != null)
            {
                return true;
            }

            if (_failed)
            {
                return false;
            }

            try
            {
                _effect = new LightingEffect(device);

                return true;
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Error($"Enhanced lighting disabled, lighting shader failed to load: {ex.Message}");

                return false;
            }
        }

        // Target for the lit world when a post-process runs after the lights
        public RenderTarget2D GetLitWorldTarget(GraphicsDevice device, int width, int height)
        {
            return _targets.GetLitWorld(device, width, height);
        }

        // Draws the collected lights into the light map, then writes world x light into destination.
        // world and destination must be different targets: binding a target clears it.
        public void Draw(
            UltimaBatcher2D batcher,
            LightManager lights,
            Texture2D world,
            RenderTarget2D destination,
            Matrix worldMatrix,
            Vector3 ambient,
            float lightMapScale,
            float intensity
        )
        {
            GraphicsDevice device = batcher.GraphicsDevice;
            RenderTarget2D lightMap = _targets.GetLightMap(device, world.Width, world.Height, lightMapScale);

            device.SetRenderTarget(lightMap);

            Vector3 encodedAmbient = Vector3.Clamp(ambient, Vector3.Zero, Vector3.One) * 0.5f;
            device.Clear(ClearOptions.Target, new Vector4(encodedAmbient, 1f), 0f, 0);

            Matrix toLightMap = worldMatrix * Matrix.CreateScale(
                lightMap.Width / (float)world.Width,
                lightMap.Height / (float)world.Height,
                1f
            );

            batcher.Begin(null, toLightMap);
            batcher.SetBlendState(_halfAdditive);
            batcher.SetSampler(SamplerState.LinearClamp);
            lights.Draw(batcher);
            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();

            device.SetRenderTarget(destination);

            _effect.CurrentTechnique = _effect.Composite;
            _effect.SetProjection(Matrix.Identity);
            _effect.WorldTexture.SetValue(world);
            _effect.OverbrightLimit.SetValue(OVERBRIGHT_LIMIT * MathHelper.Clamp(intensity, 0f, 2f));
            _effect.LightSaturation.SetValue(LIGHT_SATURATION);

            // the light map is stretched over the target with linear filtering, the world is 1:1
            batcher.Begin(_effect, Matrix.Identity);
            batcher.SetBlendState(BlendState.Opaque);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(lightMap, new Rectangle(0, 0, destination.Width, destination.Height), Vector3.UnitZ, 0f);
            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();

            // the world target is drawn into again next frame: do not leave it bound as a texture
            _effect.WorldTexture.SetValue((Texture2D) null);
            device.Textures[1] = null;
            device.SetRenderTarget(null);
        }

        public void Dispose()
        {
            _targets.Dispose();
            _effect?.Dispose();
            _effect = null;
        }
    }
}
