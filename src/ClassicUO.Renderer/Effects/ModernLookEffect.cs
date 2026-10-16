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
            LitScene = Parameters["LitScene"];
            GlowAmount = Parameters["GlowAmount"];
            GlowAmount.SetValue(1f);
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter TextureSize { get; }
        public EffectParameter Strength { get; }
        // 1 when enhanced lighting already lit the image (highlight protection), 0 otherwise
        public EffectParameter LitScene { get; }
        // 0 while the bloom pass draws the glow instead
        public EffectParameter GlowAmount { get; }
    }
}
