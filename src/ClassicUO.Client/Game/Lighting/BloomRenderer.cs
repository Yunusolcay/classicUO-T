// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Lighting
{
    // Bloom (docs/BLOOM_PLAN.md): the bright parts of the finished world image, averaged down to a
    // quarter size and blurred twice, are added back on top. Torches, fires, lava and spells get a
    // soft glow; with enhanced lighting the lit areas around lights glow too.
    internal sealed class BloomRenderer : IDisposable
    {
        private const int DOWNSAMPLE = 4;
        private const int BLUR_ITERATIONS = 2;

        // color = color + bloom * strength
        private static readonly BlendState _additive = new BlendState
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One
        };

        private LightingEffect _effect;
        private RenderTarget2D _bloom, _blur;
        private bool _failed;
        private bool _ready;
        private float _strength;

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
                Log.Error($"Bloom disabled, lighting shader failed to load: {ex.Message}");

                return false;
            }
        }

        // Builds the blurred bright image from source. threshold: luma where the glow starts.
        public void Prepare(UltimaBatcher2D batcher, Texture2D source, float threshold, float strength)
        {
            GraphicsDevice device = batcher.GraphicsDevice;
            int width = Math.Max(1, source.Width / DOWNSAMPLE);
            int height = Math.Max(1, source.Height / DOWNSAMPLE);

            EnsureTarget(device, ref _bloom, width, height);
            EnsureTarget(device, ref _blur, width, height);

            _effect.BloomThreshold.SetValue(threshold);
            _effect.TexelSize.SetValue(new Vector2(1f / source.Width, 1f / source.Height));
            DrawPass(batcher, source, _bloom, _effect.BrightPass);

            _effect.TexelSize.SetValue(new Vector2(1f / width, 1f / height));

            for (int i = 0; i < BLUR_ITERATIONS; i++)
            {
                DrawPass(batcher, _bloom, _blur, _effect.BlurH);
                DrawPass(batcher, _blur, _bloom, _effect.BlurV);
            }

            _strength = strength;
            _ready = true;
        }

        // Adds the bloom to the bound target; call with nothing else begun on the batcher
        public void DrawAdditive(UltimaBatcher2D batcher, int targetWidth, int targetHeight)
        {
            if (!_ready)
            {
                return;
            }

            batcher.Begin();
            batcher.SetBlendState(_additive);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(_bloom, new Rectangle(0, 0, targetWidth, targetHeight), new Vector3(0, 0, _strength), 0f);
            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();

            _ready = false;
        }

        // No other post-process runs: copies source into destination with the bloom on top
        public void Composite(UltimaBatcher2D batcher, Texture2D source, RenderTarget2D destination)
        {
            GraphicsDevice device = batcher.GraphicsDevice;
            device.SetRenderTarget(destination);

            batcher.Begin();
            batcher.SetBlendState(BlendState.Opaque);
            batcher.Draw(source, new Rectangle(0, 0, destination.Width, destination.Height), Vector3.UnitZ, 0f);
            batcher.SetBlendState(null);
            batcher.End();

            DrawAdditive(batcher, destination.Width, destination.Height);

            device.SetRenderTarget(null);
        }

        private void DrawPass(UltimaBatcher2D batcher, Texture2D source, RenderTarget2D target, EffectTechnique technique)
        {
            batcher.GraphicsDevice.SetRenderTarget(target);

            _effect.CurrentTechnique = technique;
            _effect.SetProjection(Matrix.Identity);

            batcher.Begin(_effect, Matrix.Identity);
            batcher.SetBlendState(BlendState.Opaque);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(source, new Rectangle(0, 0, target.Width, target.Height), Vector3.UnitZ, 0f);
            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();

            batcher.GraphicsDevice.SetRenderTarget(null);
        }

        private static void EnsureTarget(GraphicsDevice device, ref RenderTarget2D target, int width, int height)
        {
            if (target != null && !target.IsDisposed && target.Width == width && target.Height == height)
            {
                return;
            }

            target?.Dispose();
            target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
        }

        public void Dispose()
        {
            _bloom?.Dispose();
            _bloom = null;
            _blur?.Dispose();
            _blur = null;
            _effect?.Dispose();
            _effect = null;
        }
    }
}
