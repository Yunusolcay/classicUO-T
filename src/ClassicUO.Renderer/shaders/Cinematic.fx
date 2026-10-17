// SPDX-License-Identifier: BSD-2-Clause
// Cinematic look (docs/CINEMATIC_PLAN.md): the last pass on the finished world image. A film tone
// curve with split toning (cool shadows, warm highlights), local contrast, a tilt-shift depth of
// field around the player, aerial haze towards the top of the screen and a vignette.
//
// Down, BlurH and BlurV build a soft half size copy of the image. Composite draws that copy
// stretched over the target (s0, linear) and reads the sharp image 1:1 on s3, where the batcher
// samples by point. s1 and s2 hold the hue tables for the whole session (Client.cs).

float4x4 MatrixTransform;
// one texel of the pass source in UV units
float2 TexelSize;
// 0 = original image, 1 = full grade
float Strength;
// 0 = everything sharp and no haze, 1 = full tilt-shift and haze
float DepthAmount;
// vertical position (UV) of the player: the sharp band of the tilt-shift
float FocusY;
// 0 at night .. 1 in daylight: the haze is lighter by day and darker at night
float Daylight;

sampler Source : register(s0);

texture SharpTexture;
sampler Sharp : register(s3) = sampler_state
{
    Texture = <SharpTexture>;
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
};

PS_INPUT main_vertex(VS_INPUT IN)
{
    PS_INPUT OUT;
    OUT.Position = mul(IN.Position, MatrixTransform);
    OUT.TexCoord = IN.TexCoord;

    return OUT;
}

// four linear taps one source texel apart: a smooth average for the half size target
float4 down(PS_INPUT IN) : COLOR0
{
    float2 uv = IN.TexCoord.xy;
    float2 h = TexelSize;

    float3 c = tex2D(Source, uv + float2(-h.x, -h.y)).rgb;
    c += tex2D(Source, uv + float2(h.x, -h.y)).rgb;
    c += tex2D(Source, uv + float2(-h.x, h.y)).rgb;
    c += tex2D(Source, uv + float2(h.x, h.y)).rgb;

    return float4(c * 0.25, 1.0);
}

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

float4 composite(PS_INPUT IN) : COLOR0
{
    float2 uv = IN.TexCoord.xy;
    float3 original = tex2D(Sharp, uv).rgb;
    float3 soft = tex2D(Source, uv).rgb;
    float3 c = original;

    // tilt-shift: a sharp band through the player, blurring towards the top and bottom edges (a
    // little towards the sides too), like a photo of a miniature; never fully out of focus
    float band = abs(uv.y - FocusY) + abs(uv.x - 0.5) * 0.25;
    float defocus = smoothstep(0.20, 0.62, band) * 0.85 * DepthAmount;

    // local contrast: material and shape detail stand out where the image is sharp
    c += (c - soft) * 0.35 * (1.0 - defocus) * Strength;
    c = lerp(c, soft, defocus);

    // aerial perspective: the top of the screen is the far side of the isometric view; it fades
    // half way into a cool haze, light by day and dim at night
    float far = saturate((FocusY - uv.y) / max(FocusY, 0.01));
    float haze = far * far * 0.18 * DepthAmount;
    float3 hazeColor = lerp(float3(0.10, 0.12, 0.17), float3(0.70, 0.78, 0.88), Daylight);
    c = lerp(c, c * 0.5 + hazeColor * 0.5, haze);

    // film tone: UO scenes are dark, so the mid tones are lifted first, then a gentle S-curve
    c = pow(max(saturate(c), 0.0001), 1.0 - 0.22 * Strength);
    float3 curve = c * c * (3.0 - 2.0 * c);
    c = lerp(c, curve, 0.30 * Strength);

    // split toning: teal shadows, warm amber highlights
    float luma = dot(c, LUMA);
    float3 shadowTint = float3(-0.030, 0.008, 0.040);
    float3 highlightTint = float3(0.080, 0.035, -0.045);
    c += (shadowTint * (1.0 - luma) * (1.0 - luma) + highlightTint * luma * luma) * Strength;

    // richer color: everything a little, dull colors more than already saturated ones
    luma = dot(c, LUMA);
    float sat = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
    c = lerp(luma.xxx, c, 1.0 + (0.15 + 0.35 * (1.0 - saturate(sat))) * Strength);

    // vignette around the player
    float2 v = (uv - float2(0.5, FocusY)) * float2(1.0, 1.15);
    c *= 1.0 - 0.30 * smoothstep(0.35, 0.90, length(v)) * Strength;

    return float4(saturate(c), 1.0);
}

technique Down
{
    pass P0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 down();
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
