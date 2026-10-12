// SPDX-License-Identifier: BSD-2-Clause
// Post-process for the "Modern look" option: applied to the finished world image (before lights
// and UI). Local sharpening, a soft glow around bright areas, color grading and a light vignette.
float2 textureSize;
float4x4 MatrixTransform;
// 0 = original image, 1 = full effect
float Strength;

sampler world : register(s0);

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
};

PS_INPUT main_vertex(VS_INPUT IN)
{
    PS_INPUT OUT;
    OUT.Position = mul(IN.Position, MatrixTransform);
    OUT.TexCoord = IN.TexCoord;

    return OUT;
}

float3 bright(float2 uv)
{
    float3 c = tex2D(world, uv).rgb;

    // only what is clearly brighter than the scene glows: lava, fire, lights, sunlit snow
    return c * saturate((dot(c, LUMA) - 0.62) * 3.0);
}

float4 main_fragment(PS_INPUT IN) : COLOR0
{
    float2 uv = IN.TexCoord.xy;
    float2 px = 1.0 / textureSize;
    float3 original = tex2D(world, uv).rgb;
    float3 c = original;

    // sharpen: crisper material detail, mostly visible on the remastered textures
    float3 around = tex2D(world, uv + float2(px.x, 0)).rgb + tex2D(world, uv - float2(px.x, 0)).rgb
                  + tex2D(world, uv + float2(0, px.y)).rgb + tex2D(world, uv - float2(0, px.y)).rgb;
    c += (c - around * 0.25) * 0.5;

    // glow: two rings of samples around the pixel
    float3 glow = 0;
    float2 r1 = px * 3.0;
    float2 r2 = px * 7.0;
    glow += bright(uv + float2( r1.x, 0)) + bright(uv + float2(-r1.x, 0));
    glow += bright(uv + float2(0,  r1.y)) + bright(uv + float2(0, -r1.y));
    glow += bright(uv + float2( r2.x,  r2.y) * 0.707) + bright(uv + float2(-r2.x,  r2.y) * 0.707);
    glow += bright(uv + float2( r2.x, -r2.y) * 0.707) + bright(uv + float2(-r2.x, -r2.y) * 0.707);
    glow += bright(uv + float2( r2.x, 0)) + bright(uv + float2(-r2.x, 0));
    glow += bright(uv + float2(0,  r2.y)) + bright(uv + float2(0, -r2.y));
    c += glow * (0.5 / 12.0);

    // color grading: a little brighter (UO scenes are dark), vibrance (dull colors gain more than
    // already saturated ones), a gentle S-curve for contrast, cool shadows and warm highlights
    c = pow(saturate(c), 0.85);
    float luma = dot(c, LUMA);
    float sat = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
    c = lerp(luma.xxx, c, 1.0 + 0.55 * (1.0 - saturate(sat)));
    c = saturate(c);
    c = lerp(c, c * c * (3.0 - 2.0 * c), 0.25);
    c += (luma - 0.45) * float3(0.05, 0.015, -0.05);

    // vignette: draws the eye to the middle of the screen, where the player is
    float d = distance(uv, float2(0.5, 0.5));
    c *= 1.0 - 0.22 * smoothstep(0.42, 0.85, d);

    return float4(lerp(original, saturate(c), Strength), 1.0);
}

technique T0
{
    pass P0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 main_fragment();
    }
}
