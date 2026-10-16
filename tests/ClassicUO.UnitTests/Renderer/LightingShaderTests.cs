using System;
using System.Text;
using ShaderResources = ClassicUO.Renderer.Resources;
using Xunit;

namespace ClassicUO.UnitTests.Renderer
{
    public class LightingShaderTests
    {
        [Fact]
        public void Embedded_Shader_Is_An_Fx_2_0_Effect()
        {
            ReadOnlySpan<byte> shader = ShaderResources.GetLightingShader();

            Assert.True(shader.Length > 4);
            // fx_2_0 effect binary: version token 0xFEFF0901, little endian
            Assert.Equal(0xFEFF0901u, BitConverter.ToUInt32(shader.Slice(0, 4)));
        }

        [Theory]
        [InlineData("LightTexture")]
        [InlineData("LightRadial")]
        [InlineData("BlurH")]
        [InlineData("BlurV")]
        [InlineData("Composite")]
        [InlineData("MatrixTransform")]
        [InlineData("TexelSize")]
        [InlineData("FalloffPower")]
        [InlineData("OverbrightLimit")]
        [InlineData("LightSaturation")]
        [InlineData("WorldTexture")]
        public void Embedded_Shader_Has_The_Names_LightingEffect_Looks_Up(string name)
        {
            // LightingEffect reads these techniques and parameters by name; a stale .fxc would miss them
            string blob = Encoding.ASCII.GetString(ShaderResources.GetLightingShader());

            Assert.Contains(name, blob);
        }
    }
}
