// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    // shaders/Lighting.fx: light sprites into the light map, the light map blur passes and the composite
    public class LightingEffect : Effect
    {
        public LightingEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Resources.GetLightingShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            TexelSize = Parameters["TexelSize"];
            FalloffPower = Parameters["FalloffPower"];
            OverbrightLimit = Parameters["OverbrightLimit"];
            LightSaturation = Parameters["LightSaturation"];
            WorldTexture = Parameters["WorldTexture"];
            BloomThreshold = Parameters["BloomThreshold"];

            LightTexture = Techniques["LightTexture"];
            LightRadial = Techniques["LightRadial"];
            BlurH = Techniques["BlurH"];
            BlurV = Techniques["BlurV"];
            Composite = Techniques["Composite"];
            BrightPass = Techniques["BrightPass"];

            CurrentTechnique = LightTexture;
            FalloffPower.SetValue(2f);
            OverbrightLimit.SetValue(0.35f);
            LightSaturation.SetValue(0.75f);
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter TexelSize { get; }
        public EffectParameter FalloffPower { get; }
        public EffectParameter OverbrightLimit { get; }
        public EffectParameter LightSaturation { get; }
        public EffectParameter WorldTexture { get; }
        public EffectParameter BloomThreshold { get; }

        // light.mul shapes from the light atlas, tinted by the vertex RGB
        public EffectTechnique LightTexture { get; }
        // procedural round light over the whole quad
        public EffectTechnique LightRadial { get; }
        public EffectTechnique BlurH { get; }
        public EffectTechnique BlurV { get; }
        // light map over the target (s0) multiplied into WorldTexture (s3) with a soft overbright limit
        public EffectTechnique Composite { get; }
        // bloom: bright parts of the source, averaged down to a quarter size
        public EffectTechnique BrightPass { get; }

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
