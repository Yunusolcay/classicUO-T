using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Effects
{
    internal class BasicUOEffect : Effect
    {
        public BasicUOEffect(GraphicsDevice graphicsDevice) : base(graphicsDevice, Resources.GetUOShader().ToArray())
        {
            MatrixTransform = Parameters["MatrixTransform"];
            WorldMatrix = Parameters["WorldMatrix"];
            Viewport = Parameters["Viewport"];
            Brighlight = Parameters["Brightlight"];
            CircleOfTransparencyRadius = Parameters["CircleOfTransparencyRadius"];
            Time = Parameters["Time"];
            WorldOrigin = Parameters["WorldOrigin"];
            AmbientTint = Parameters["AmbientTint"];
            AmbientTint?.SetValue(Microsoft.Xna.Framework.Vector3.One);
            SunDirection = Parameters["SunDirection"];
            SurfaceLight = Parameters["SurfaceLight"];
            SurfaceLight?.SetValue(0f);

            CurrentTechnique = Techniques["HueTechnique"];
            Pass = CurrentTechnique.Passes[0];
        }

        public EffectParameter MatrixTransform { get; }
        public EffectParameter WorldMatrix { get; }
        public EffectParameter Viewport { get; }
        public EffectParameter Brighlight { get; }
        public EffectParameter CircleOfTransparencyRadius { get; }
        public EffectParameter Time { get; }
        public EffectParameter WorldOrigin { get; }
        public EffectParameter AmbientTint { get; }
        public EffectParameter SunDirection { get; }
        public EffectParameter SurfaceLight { get; }
        public EffectPass Pass { get; }
    }
}
