// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Lighting
{
    // Cinematic look (docs/CINEMATIC_PLAN.md): the last pass on the world image. A half size soft
    // copy is made first (down + two blur iterations); the composite then grades the sharp image and
    // mixes in the soft copy for local contrast, the tilt-shift depth of field and the haze.
    internal sealed class CinematicRenderer : IDisposable
    {
        private const int DOWNSAMPLE = 2;
        private const int BLUR_ITERATIONS = 2;
        // sampler register of SharpTexture in Cinematic.fx
        private const int SHARP_TEXTURE_SLOT = 3;

        private CinematicEffect _effect;
        private RenderTarget2D _source, _soft, _blur;
        private bool _failed;

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
                _effect = new CinematicEffect(device);

                return true;
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Error($"Cinematic look disabled, shader failed to load: {ex.Message}");

                return false;
            }
        }

        // Where the other passes write the world image when the cinematic pass comes after them
        public RenderTarget2D GetSourceTarget(GraphicsDevice device, int width, int height)
        {
            EnsureTarget(device, ref _source, width, height);

            return _source;
        }

        public void Apply(UltimaBatcher2D batcher, RenderTarget2D source, RenderTarget2D destination, float focusY, float daylight, bool depth)
        {
            GraphicsDevice device = batcher.GraphicsDevice;
            int width = Math.Max(1, source.Width / DOWNSAMPLE);
            int height = Math.Max(1, source.Height / DOWNSAMPLE);

            EnsureTarget(device, ref _soft, width, height);
            EnsureTarget(device, ref _blur, width, height);

            _effect.TexelSize.SetValue(new Vector2(1f / source.Width, 1f / source.Height));
            DrawPass(batcher, source, _soft, _effect.Down);

            _effect.TexelSize.SetValue(new Vector2(1f / width, 1f / height));

            for (int i = 0; i < BLUR_ITERATIONS; i++)
            {
                DrawPass(batcher, _soft, _blur, _effect.BlurH);
                DrawPass(batcher, _blur, _soft, _effect.BlurV);
            }

            device.SetRenderTarget(destination);

            _effect.CurrentTechnique = _effect.Composite;
            _effect.SetProjection();
            _effect.Strength.SetValue(1f);
            _effect.DepthAmount.SetValue(depth ? 1f : 0f);
            _effect.FocusY.SetValue(MathHelper.Clamp(focusY, 0.2f, 0.8f));
            _effect.Daylight.SetValue(MathHelper.Clamp(daylight, 0f, 1f));
            _effect.SharpTexture.SetValue(source);

            // the soft copy is stretched over the target with linear filtering, the sharp image is 1:1
            batcher.Begin(_effect, Matrix.Identity);
            batcher.SetBlendState(BlendState.Opaque);
            batcher.SetSampler(SamplerState.LinearClamp);
            batcher.Draw(_soft, new Rectangle(0, 0, destination.Width, destination.Height), Vector3.UnitZ, 0f);
            batcher.SetSampler(null);
            batcher.SetBlendState(null);
            batcher.End();

            // the source is drawn into again next frame: do not leave it bound as a texture.
            // Only slot 3: slots 1 and 2 are the hue tables bound once at startup.
            _effect.SharpTexture.SetValue((Texture2D) null);
            device.Textures[SHARP_TEXTURE_SLOT] = null;
            device.SetRenderTarget(null);
        }

        private void DrawPass(UltimaBatcher2D batcher, Texture2D source, RenderTarget2D target, EffectTechnique technique)
        {
            batcher.GraphicsDevice.SetRenderTarget(target);

            _effect.CurrentTechnique = technique;
            _effect.SetProjection();

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
            _source?.Dispose();
            _source = null;
            _soft?.Dispose();
            _soft = null;
            _blur?.Dispose();
            _blur = null;
            _effect?.Dispose();
            _effect = null;
        }
    }
}
