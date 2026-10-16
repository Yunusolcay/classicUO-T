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
        // sampler register of WorldTexture in Lighting.fx
        private const int WORLD_TEXTURE_SLOT = 3;
        private const int ROUND_LIGHT_SIZE = 128;

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
        private Texture2D _roundLight;
        // stands in for the world in the light map only debug view
        private Texture2D _white;
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
                _roundLight = CreateRoundLight(device, ROUND_LIGHT_SIZE);
                _white = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
                _white.SetData(new[] { Color.White });

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
            int blurPasses,
            float intensity,
            bool flicker,
            bool proceduralFalloff,
            in PersonalHalo halo
        )
        {
            GraphicsDevice device = batcher.GraphicsDevice;
            RenderTarget2D lightMap = _targets.GetLightMap(device, world.Width, world.Height, lightMapScale);

            device.SetRenderTarget(lightMap);
            _lastLightCount = lights.Count;
            _lastLightMaxCount = lights.MaxLights;

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
            lights.Draw(
                batcher,
                new LightDrawOptions(true, flicker, proceduralFalloff ? _roundLight : null, Time.Ticks)
            );

            if (halo.Intensity > 0f)
            {
                int size = halo.Diameter;

                // plain white light: hue mode none, the alpha is the strength
                batcher.Draw(
                    _roundLight,
                    new Rectangle(halo.X - size / 2, halo.Y - size / 2, size, size),
                    new Vector3(0f, 0f, Math.Clamp(halo.Intensity, 0f, 1f)),
                    0f
                );
            }

            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();

            DrawEffectLights(batcher, toLightMap);

            // each blur pair: horizontal into the blur target, vertical back into the light map
            for (int i = 0; i + 1 < blurPasses; i += 2)
            {
                RenderTarget2D blur = _targets.GetBlurTarget(device, lightMap.Width, lightMap.Height);

                DrawBlurPass(batcher, lightMap, blur, _effect.BlurH);
                DrawBlurPass(batcher, blur, lightMap, _effect.BlurV);
            }

            device.SetRenderTarget(destination);

            _effect.CurrentTechnique = _effect.Composite;
            _effect.SetProjection(Matrix.Identity);
            _effect.WorldTexture.SetValue(LightingDebug.ShowLightMapOnly ? _white : world);
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

            // the world target is drawn into again next frame: do not leave it bound as a texture.
            // Only slot 3: slots 1 and 2 are the hue tables bound once at startup.
            _effect.WorldTexture.SetValue((Texture2D) null);
            device.Textures[WORLD_TEXTURE_SLOT] = null;
            device.SetRenderTarget(null);
        }

        private void DrawBlurPass(UltimaBatcher2D batcher, RenderTarget2D source, RenderTarget2D target, EffectTechnique technique)
        {
            batcher.GraphicsDevice.SetRenderTarget(target);

            _effect.CurrentTechnique = technique;
            _effect.SetProjection(Matrix.Identity);
            _effect.TexelSize.SetValue(new Vector2(1f / source.Width, 1f / source.Height));

            batcher.Begin(_effect, Matrix.Identity);
            batcher.SetBlendState(BlendState.Opaque);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(source, new Rectangle(0, 0, target.Width, target.Height), Vector3.UnitZ, 0f);
            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();
        }

        // Spell/effect lights (docs/EFFECTS_PLAN.md): plain RGB round lights, LightingEffect.LightRadial
        // takes the color from the vertex hue. Same half strength encoding as the other lights.
        private void DrawEffectLights(UltimaBatcher2D batcher, Matrix toLightMap)
        {
            ReadOnlySpan<EffectLight> lights = EffectLights.Lights;

            if (lights.IsEmpty)
            {
                return;
            }

            _effect.CurrentTechnique = _effect.LightRadial;
            _effect.SetProjection(toLightMap);

            batcher.Begin(_effect, Matrix.Identity);
            batcher.SetBlendState(_halfAdditive);

            foreach (ref readonly EffectLight light in lights)
            {
                int size = (int)(light.Radius * 2f);

                batcher.Draw(
                    _roundLight,
                    new Rectangle((int)light.Position.X - size / 2, (int)light.Position.Y - size / 2, size, size),
                    light.Color,
                    0f
                );
            }

            batcher.SetBlendState(null);
            batcher.End();

            EffectLights.Clear();
        }

        // Gray radial gradient read like a light.mul shape: the light color tables map its red
        // channel to the light color. Full at the center, smooth to zero at the edge.
        private static Texture2D CreateRoundLight(GraphicsDevice device, int size)
        {
            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float falloff = 1f - Math.Clamp(dx * dx + dy * dy, 0f, 1f);
                    byte value = (byte)(falloff * falloff * 255f + 0.5f);

                    // alpha stays opaque: the world shader discards fully transparent texels
                    pixels[y * size + x] = new Color(value, value, value, (byte)255);
                }
            }

            Texture2D texture = new Texture2D(device, size, size, false, SurfaceFormat.Color);
            texture.SetData(pixels);

            return texture;
        }

        // lights drawn by the last Draw, for the debug overlay
        public int LastLightCount => _lastLightCount;
        public int LastMaxLights => _lastLightMaxCount;

        private int _lastLightCount, _lastLightMaxCount;

        public void Dispose()
        {
            _targets.Dispose();
            _roundLight?.Dispose();
            _roundLight = null;
            _white?.Dispose();
            _white = null;
            _effect?.Dispose();
            _effect = null;
        }
    }
}
