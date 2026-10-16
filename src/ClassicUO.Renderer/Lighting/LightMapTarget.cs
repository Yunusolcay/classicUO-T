// SPDX-License-Identifier: BSD-2-Clause

using System;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Lighting
{
    // Render targets of the enhanced lighting: the light map (a fraction of the world size) and the
    // lit world image used when a post-process still runs after the lights. Both are fully redrawn
    // every frame, so they need no depth buffer and may discard their contents.
    public sealed class LightMapTarget : IDisposable
    {
        private RenderTarget2D _lightMap;
        private RenderTarget2D _litWorld;

        public RenderTarget2D GetLightMap(GraphicsDevice device, int worldWidth, int worldHeight, float scale)
        {
            int width = Math.Max(1, (int)Math.Ceiling(worldWidth * scale));
            int height = Math.Max(1, (int)Math.Ceiling(worldHeight * scale));

            EnsureSize(device, ref _lightMap, width, height);

            return _lightMap;
        }

        public RenderTarget2D GetLitWorld(GraphicsDevice device, int width, int height)
        {
            EnsureSize(device, ref _litWorld, width, height);

            return _litWorld;
        }

        private static void EnsureSize(GraphicsDevice device, ref RenderTarget2D target, int width, int height)
        {
            if (target != null && !target.IsDisposed && target.Width == width && target.Height == height)
            {
                return;
            }

            target?.Dispose();
            target = new RenderTarget2D(
                device,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.DiscardContents
            );
        }

        public void Dispose()
        {
            _lightMap?.Dispose();
            _lightMap = null;
            _litWorld?.Dispose();
            _litWorld = null;
        }
    }
}
