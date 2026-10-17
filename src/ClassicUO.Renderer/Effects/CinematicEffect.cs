// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    // shaders/Cinematic.fx: blur passes for the soft copy and the composite with the film grade,
    // tilt-shift depth of field, aerial haze and vignette
    public class CinematicEffect : Effect
    {
        public CinematicEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Resources.GetCinematicShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            TexelSize = Parameters["TexelSize"];
            Strength = Parameters["Strength"];
            DepthAmount = Parameters["DepthAmount"];
            FocusY = Parameters["FocusY"];
            Daylight = Parameters["Daylight"];
            SharpTexture = Parameters["SharpTexture"];

            Down = Techniques["Down"];
            BlurH = Techniques["BlurH"];
            BlurV = Techniques["BlurV"];
            Composite = Techniques["Composite"];

            CurrentTechnique = Composite;
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter TexelSize { get; }
        public EffectParameter Strength { get; }
        public EffectParameter DepthAmount { get; }
        public EffectParameter FocusY { get; }
        public EffectParameter Daylight { get; }
        // the sharp image, read 1:1 on s3
        public EffectParameter SharpTexture { get; }

        // average into a half size target
        public EffectTechnique Down { get; }
        public EffectTechnique BlurH { get; }
        public EffectTechnique BlurV { get; }
        // soft copy stretched over the target (s0) graded together with SharpTexture (s3)
        public EffectTechnique Composite { get; }

        // the batcher does not set the projection on custom effects; call after the target is set
        public void SetProjection()
        {
            Viewport viewport = GraphicsDevice.Viewport;

            MatrixTransform.SetValue(Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, short.MinValue, short.MaxValue));
        }
    }
}
