#define NONE 0
#define HUED 1
#define PARTIAL_HUED 2
#define HUE_TEXT_NO_BLACK 3
#define HUE_TEXT 4
#define LAND 5
#define LAND_COLOR 6
#define SPECTRAL 7
#define SHADOW 8
#define LIGHTS 9
#define EFFECT_HUED 10
#define NATURE_GRASS 11
#define NATURE_GRASS_LIT 12
#define GUMP 20

const static float3 LIGHT_DIRECTION = float3(0.0f, 1.0f, 1.0f);

const static float HUE_ROWS = 1024;
const static float HUE_COLUMNS = 16;
const static float HUE_WIDTH = 32;
const static float HUES_PER_TEXTURE = HUE_ROWS * HUE_COLUMNS;

float4x4 MatrixTransform;
float4x4 WorldMatrix;
float2 Viewport;
float Brightlight;
float CircleOfTransparencyRadius;
// seconds, drives the nature effects wind
float Time;

sampler DrawSampler : register(s0);
sampler HueSampler0 : register(s1);
sampler HueSampler1 : register(s2);

struct VS_INPUT
{
	float4 Position : POSITION0;
	float3 Normal	: NORMAL0;
	float3 TexCoord : TEXCOORD0;
	float3 Hue		: TEXCOORD1;
};

struct PS_INPUT
{
	float4 Position : POSITION0;
	float3 TexCoord : TEXCOORD0;
	float3 Normal	: TEXCOORD1;
	float3 Hue		: TEXCOORD2;
	float3 PixelPos : TEXCOORD3;
};

float3 get_rgb(float gray, float hue)
{
    float halfPixelX = (1.0f / (HUE_COLUMNS * HUE_WIDTH)) * 0.5f;
    float hueColumnWidth = 1.0f / HUE_COLUMNS;
    float hueStart = frac(hue / HUE_COLUMNS);
    
    float xPos = hueStart + gray / HUE_COLUMNS;
    xPos = clamp(xPos, hueStart + halfPixelX, hueStart + hueColumnWidth - halfPixelX);
    float yPos = (hue % HUES_PER_TEXTURE) / (HUES_PER_TEXTURE - 1);
    return tex2D(HueSampler0, float2(xPos, yPos)).rgb;
}

float get_light(float3 norm)
{
	float3 light = normalize(LIGHT_DIRECTION);
	float3 normal = normalize(norm);
	float base = (max(dot(normal, light), 0.0f) / 2.0f) + 0.5f;

	// At 45 degrees (the angle the flat tiles are lit at) it must come out
	// to (cos(45) / 2) + 0.5 or 0.85355339...
	return base + ((Brightlight * (base - 0.85355339f)) - (base - 0.85355339f));
}

float3 get_colored_light(float shader, float gray)
{
	float2 texcoord = float2(gray, (shader - 0.5) / 63);

	return tex2D(HueSampler1, texcoord).rgb;
}

// Livelier grass: more saturation and a slightly brighter, varied green, applied only to green
// pixels so dirt in grass/dirt transition tiles and flowers keep their colors.
float3 grade_grass(float3 color, float tint)
{
	float greenness = saturate((color.g - max(color.r, color.b)) * 8.0f);
	float luma = dot(color, float3(0.299f, 0.587f, 0.114f));
	float3 vivid = lerp(float3(luma, luma, luma), color, 1.3f);
	vivid *= float3(1.0f + 0.05f * tint, 1.07f + 0.04f * tint, 0.96f - 0.03f * tint);

	return lerp(color, saturate(vivid), greenness);
}

// Soft shadow (docs/SHADOWS_PLAN.md, S1): Hue.x = strength (> 0 marks a soft shadow), Normal =
// sprite bounds (u min, v min, u max), Hue.z = v max. Coverage is averaged over a disc that grows
// from the base (bottom of the sprite, at the feet) to the far end (top of the sprite). Samples
// stay inside the sprite: the atlas packs sprites without gaps.
float soft_shadow_tap(float2 uv, float2 lo, float2 hi)
{
	return tex2Dlod(DrawSampler, float4(clamp(uv, lo, hi), 0, 0)).a;
}

float4 soft_shadow(PS_INPUT IN, float2 dx, float2 dy)
{
	float2 lo = IN.Normal.xy;
	float2 hi = float2(IN.Normal.z, IN.Hue.z);
	float2 uv = IN.TexCoord.xy;

	float far = 1.0f - saturate((uv.y - lo.y) / max(hi.y - lo.y, 0.00001f));
	float radius = lerp(0.6f, 2.2f, far);
	float2 sx = dx * radius;
	float2 sy = dy * radius;
	float2 d1 = (sx + sy) * 0.7071f;
	float2 d2 = (sx - sy) * 0.7071f;

	float a = soft_shadow_tap(uv, lo, hi) * 0.2f;
	a += (soft_shadow_tap(uv + sx, lo, hi) + soft_shadow_tap(uv - sx, lo, hi)
		+ soft_shadow_tap(uv + sy, lo, hi) + soft_shadow_tap(uv - sy, lo, hi)) * 0.125f;
	a += (soft_shadow_tap(uv + d1, lo, hi) + soft_shadow_tap(uv - d1, lo, hi)
		+ soft_shadow_tap(uv + d2, lo, hi) + soft_shadow_tap(uv - d2, lo, hi)) * 0.075f;

	a *= IN.Hue.x;

	if (a < 0.004f)
		discard;

	return float4(0, 0, 0, a);
}

PS_INPUT VertexShaderFunction(VS_INPUT IN)
{
	PS_INPUT OUT;

	float4 position = IN.Position;

	// Wind sway: TexCoord.z holds how many pixels this vertex may move (only the top vertices of
	// plant/tree sprites are non zero) and Normal.x the per-tile phase. Whole pixel steps keep the
	// pixel art crisp with point sampling.
	if (IN.TexCoord.z > 0.0f)
	{
		float wave = sin(Time * 1.6f + IN.Normal.x);
		float gust = 0.7f + 0.3f * sin(Time * 0.35f + IN.Normal.x * 0.15f);
		position.x += floor(wave * gust * IN.TexCoord.z + 0.5f);
	}

	OUT.Position = mul(mul(position, WorldMatrix), MatrixTransform);
	
	OUT.Position.x -= 0.5 / Viewport.x;
	OUT.Position.y += 0.5 / Viewport.y;

	OUT.TexCoord = IN.TexCoord;
	OUT.Normal = IN.Normal;
	OUT.Hue = IN.Hue;
	OUT.PixelPos = OUT.Position.xyz;

	return OUT;
}

float4 PixelShader_Hue(PS_INPUT IN) : COLOR0
{
	float4 color = tex2D(DrawSampler, IN.TexCoord.xy);
	// texture space size of one screen pixel, taken outside any branch
	float2 dx = ddx(IN.TexCoord.xy);
	float2 dy = ddy(IN.TexCoord.xy);

	int mode = int(IN.Hue.y);

	if (mode == SHADOW && IN.Hue.x > 0.0f)
		return soft_shadow(IN, dx, dy);

	if (color.a == 0.0f)
		discard;

	float alpha = IN.Hue.z;

	bool useTrans = false;
	if (alpha > 1.0f)
	{
		useTrans = true;
		alpha -= 1.0f;
	}

	if (alpha == 0.0f)
		discard;

	float hue = IN.Hue.x;

	if (mode >= GUMP)
	{
		mode -= GUMP;

		if (color.r < 0.02f)
		{
			hue = 0;
		}
	}

	if (mode == HUED || (mode == PARTIAL_HUED && color.r == color.g && color.r == color.b))
	{
		color.rgb = get_rgb(color.r, hue);
	}
	else if (mode == HUE_TEXT_NO_BLACK)
	{
		if (color.r > 0.04f || color.g > 0.04f || color.b > 0.04f)
		{
			color.rgb = get_rgb(1.0f, hue);
		}
	}
	else if (mode == HUE_TEXT)
	{
		// 31 is max red, so this is just selecting the color of the darkest pixel in the hue
		color.rgb = get_rgb(1.0f, hue);
	}
	else if (mode == LAND)
	{
		color.rgb *= get_light(IN.Normal);
	}
	else if (mode == LAND_COLOR)
	{
		color.rgb = get_rgb(color.r, hue) * get_light(IN.Normal);
	}
	else if (mode == SPECTRAL)
	{
		alpha = 1.0f - (color.r * 1.5f);
		color.r = 0;
		color.g = 0;
		color.b = 0;
	}
	else if (mode == SHADOW)
	{
		alpha = 0.4f;
		color.r = 0;
		color.g = 0;
		color.b = 0;
	}
	else if (mode == LIGHTS)
	{
		color.rgb = get_colored_light(IN.Hue.x - 1, color.r);
	}
	else if (mode == EFFECT_HUED)
	{
		color.rgb = get_rgb(color.g, hue);
	}
	else if (mode == NATURE_GRASS)
	{
		color.rgb = grade_grass(color.rgb, IN.Hue.x);
	}
	else if (mode == NATURE_GRASS_LIT)
	{
		color.rgb = grade_grass(color.rgb, IN.Hue.x) * get_light(IN.Normal);
	}

	if (useTrans && CircleOfTransparencyRadius > 0)
	{
		float2 pixelDist = IN.PixelPos.xy * Viewport * 0.5;
		float ratio = length(pixelDist) / CircleOfTransparencyRadius;

		if (ratio < 0.85f)
			discard;

		if (ratio < 1.0f)
		{
			float t = (ratio - 0.85f) / 0.15f;
			alpha *= t * t * t;

			if (alpha < 0.02f)
				discard;
		}
	}

	return color * alpha;
}

technique HueTechnique
{
	pass p0
	{
		VertexShader = compile vs_3_0 VertexShaderFunction();
		PixelShader = compile ps_3_0 PixelShader_Hue();
	}
}

