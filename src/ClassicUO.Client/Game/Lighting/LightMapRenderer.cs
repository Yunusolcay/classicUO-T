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
    // 0.5 in the light map leaves the scene unchanged, above it lights can brighten past the art.
    internal sealed class LightMapRenderer : IDisposable
    {
        // dst * src + src * dst = 2 * world * lightmap
        private static readonly BlendState _multiply2x = new BlendState
        {
            ColorSourceBlend = Blend.DestinationColor,
            ColorDestinationBlend = Blend.SourceColor,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One
        };

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
            float ambient,
            float lightMapScale
        )
        {
            GraphicsDevice device = batcher.GraphicsDevice;
            RenderTarget2D lightMap = _targets.GetLightMap(device, world.Width, world.Height, lightMapScale);

            device.SetRenderTarget(lightMap);

            float encodedAmbient = MathHelper.Clamp(ambient, 0f, 1f) * 0.5f;
            device.Clear(ClearOptions.Target, new Vector4(encodedAmbient, encodedAmbient, encodedAmbient, 1f), 0f, 0);

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

            Rectangle full = new Rectangle(0, 0, destination.Width, destination.Height);

            batcher.Begin();
            batcher.SetBlendState(BlendState.Opaque);
            batcher.Draw(world, full, Vector3.UnitZ, 0f);
            batcher.SetBlendState(_multiply2x);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(lightMap, full, Vector3.UnitZ, 0f);
            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();

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
