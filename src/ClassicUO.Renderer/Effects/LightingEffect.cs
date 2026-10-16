// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    // shaders/Lighting.fx: light sprites into the light map and the light map blur passes
    public class LightingEffect : Effect
    {
        public LightingEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Resources.GetLightingShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            TexelSize = Parameters["TexelSize"];
            FalloffPower = Parameters["FalloffPower"];

            LightTexture = Techniques["LightTexture"];
            LightRadial = Techniques["LightRadial"];
            BlurH = Techniques["BlurH"];
            BlurV = Techniques["BlurV"];

            CurrentTechnique = LightTexture;
            FalloffPower.SetValue(2f);
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter TexelSize { get; }
        public EffectParameter FalloffPower { get; }

        // light.mul shapes from the light atlas, tinted by the vertex RGB
        public EffectTechnique LightTexture { get; }
        // procedural round light over the whole quad
        public EffectTechnique LightRadial { get; }
        public EffectTechnique BlurH { get; }
        public EffectTechnique BlurV { get; }

        // Same projection the batcher uses for its own effect: the batcher does not set it on
        // custom effects. Call after the render target and viewport are set.
        public void SetProjection(Matrix transform)
        {
            Viewport viewport = GraphicsDevice.Viewport;
            Matrix projection = Matrix.CreateOrthographicOffCenter(
                0f,
                viewport.Width,
                viewport.Height,
                0f,
                short.MinValue,
                short.MaxValue
            );

            MatrixTransform.SetValue(transform * projection);
        }
    }
}
