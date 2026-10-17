// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Lighting
{
    // Surface lighting (docs/SURFACE_LIGHTING_PLAN.md): normal map textures that sit beside color
    // textures with the same layout, so one UV reads both. The batcher binds the normal map of the
    // texture it draws on s3; textures without one get a flat normal, which the shader leaves as is.
    public static class SurfaceNormals
    {
        // sampler register of NormalSampler in IsometricWorld.fx
        public const int SLOT = 3;

        // facing the viewer, no cavity: (128, 128, 255) normal, alpha 255
        public const uint FLAT = 0xFFFF8080;

        private static readonly Dictionary<Texture, Texture2D> _maps = new Dictionary<Texture, Texture2D>();
        private static Texture2D _flat;

        public static void Register(Texture color, Texture2D normals)
        {
            _maps[color] = normals;
        }

        public static void Unregister(Texture color)
        {
            _maps.Remove(color);
        }

        public static Texture2D Get(GraphicsDevice device, Texture color)
        {
            if (color != null && _maps.Count != 0 && _maps.TryGetValue(color, out Texture2D normals) && !normals.IsDisposed)
            {
                return normals;
            }

            if (_flat == null || _flat.IsDisposed)
            {
                _flat = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
                _flat.SetData(new[] { FLAT });
            }

            return _flat;
        }
    }
}
