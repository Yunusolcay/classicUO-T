// SPDX-License-Identifier: BSD-2-Clause
// Enhanced lighting (docs/LIGHTING_PLAN.md): light sprites drawn additively into the light map,
// a separable blur for the high quality soft light map, and the composite of the world with it.
//
// Light sprites take their color from the vertex Hue as plain RGB (intensity already applied by
// the caller, light map encoding included). Output alpha is 1 so BlendState.Additive adds the RGB.

float4x4 MatrixTransform;
// blur: one texel of the light map in UV units
float2 TexelSize;
// radial lights: higher values give a tighter core
float FalloffPower;
// composite: largest brightening above the art (0.35 = 35%), already scaled by the light intensity
float OverbrightLimit;
// composite: 1 keeps the light colors, 0 makes the lights white
float LightSaturation;

sampler Source : register(s0);

// composite: the world image, same size as the target so the batcher's point sampling on s3 is exact.
// s1 and s2 hold the hue tables for the whole session (Client.cs): never sample the world there.
texture WorldTexture;
sampler World : register(s3) = sampler_state
{
    Texture = <WorldTexture>;
};

static const float3 LUMA = float3(0.2126, 0.7152, 0.0722);

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

// Light map drawn over the whole target (s0, linear), multiplied into the world (s3). The map stores
// half the light: up to 1 the art is darkened as before, above it the extra light rolls off
// towards OverbrightLimit so overlapping lights cannot burn the art into flat color.
float4 composite(PS_INPUT IN) : COLOR0
{
    float3 light = tex2D(Source, IN.TexCoord.xy).rgb * 2.0;
    float3 world = tex2D(World, IN.TexCoord.xy).rgb;

    light = lerp(dot(light, LUMA).xxx, light, LightSaturation);

    float3 over = max(light - 1.0, 0.0);
    float limit = max(OverbrightLimit, 0.0001);
    light = min(light, 1.0) + limit * (1.0 - exp(-over / limit));

    return float4(world * light, 1.0);
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

technique Composite
{
    pass P0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 composite();
    }
}
