// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    // shaders/ModernLook.fx: color grading, glow, sharpening and vignette on the world image
    public class ModernLookEffect : Effect
    {
        public ModernLookEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Resources.GetModernLookShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            TextureSize = Parameters["textureSize"];
            Strength = Parameters["Strength"];
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter TextureSize { get; }
        public EffectParameter Strength { get; }
    }
}
