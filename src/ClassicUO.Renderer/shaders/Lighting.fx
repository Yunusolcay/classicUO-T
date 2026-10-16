// SPDX-License-Identifier: BSD-2-Clause
// Enhanced lighting (docs/LIGHTING_PLAN.md): light sprites drawn additively into the light map,
// and a separable blur for the high quality soft light map.
//
// Light sprites take their color from the vertex Hue as plain RGB (intensity already applied by
// the caller, light map encoding included). Output alpha is 1 so BlendState.Additive adds the RGB.

float4x4 MatrixTransform;
// blur: one texel of the light map in UV units
float2 TexelSize;
// radial lights: higher values give a tighter core
float FalloffPower;

sampler Source : register(s0);

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float3 TexCoord : TEXCOORD0;
    float3 Hue      : TEXCOORD1;
};

struct PS_INPUT
{
    float4 Position : POSITION0;
    float3 TexCoord : TEXCOORD0;
    float3 Hue      : TEXCOORD1;
};

PS_INPUT main_vertex(VS_INPUT IN)
{
    PS_INPUT OUT;
    OUT.Position = mul(IN.Position, MatrixTransform);
    OUT.TexCoord = IN.TexCoord;
    OUT.Hue = IN.Hue;

    return OUT;
}

// light.mul shape: the atlas holds the intensity as gray, the red channel is enough
float4 light_texture(PS_INPUT IN) : COLOR0
{
    float intensity = tex2D(Source, IN.TexCoord.xy).r;

    return float4(IN.Hue * intensity, 1.0);
}

// procedural round light over the whole quad: full at the center, zero at the edge
float4 light_radial(PS_INPUT IN) : COLOR0
{
    float2 d = IN.TexCoord.xy * 2.0 - 1.0;
    float falloff = 1.0 - saturate(dot(d, d));
    float intensity = pow(falloff, FalloffPower);

    return float4(IN.Hue * intensity, 1.0);
}

// 9 tap gaussian, one direction per pass
static const float WEIGHT0 = 0.2270270270;
static const float WEIGHT1 = 0.1945945946;
static const float WEIGHT2 = 0.1216216216;
static const float WEIGHT3 = 0.0540540541;
static const float WEIGHT4 = 0.0162162162;

float3 blur(float2 uv, float2 step)
{
    float3 c = tex2D(Source, uv).rgb * WEIGHT0;
    c += (tex2D(Source, uv + step).rgb + tex2D(Source, uv - step).rgb) * WEIGHT1;
    c += (tex2D(Source, uv + step * 2.0).rgb + tex2D(Source, uv - step * 2.0).rgb) * WEIGHT2;
    c += (tex2D(Source, uv + step * 3.0).rgb + tex2D(Source, uv - step * 3.0).rgb) * WEIGHT3;
    c += (tex2D(Source, uv + step * 4.0).rgb + tex2D(Source, uv - step * 4.0).rgb) * WEIGHT4;

    return c;
}

float4 blur_horizontal(PS_INPUT IN) : COLOR0
{
    return float4(blur(IN.TexCoord.xy, float2(TexelSize.x, 0)), 1.0);
}

float4 blur_vertical(PS_INPUT IN) : COLOR0
{
    return float4(blur(IN.TexCoord.xy, float2(0, TexelSize.y)), 1.0);
}

technique LightTexture
{
    pass P0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 light_texture();
    }
}

technique LightRadial
{
    pass P0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 light_radial();
    }
}

technique BlurH
{
    pass P0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 blur_horizontal();
    }
}

technique BlurV
{
    pass P0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 blur_vertical();
    }
}
