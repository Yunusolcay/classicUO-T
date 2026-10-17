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
#define WATER 13
#define LAND_FLAT 14
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
// seconds, drives the nature effects wind and the water
float Time;
// world pixel of the vertex position origin (the scene draw offset): makes WorldPos absolute
float2 WorldOrigin;
// water: tint of the glints (ambient light color, white without enhanced lighting)
float3 AmbientTint;
// surface lighting (docs/SURFACE_LIGHTING_PLAN.md): unit direction towards the sun in screen space
// (x right, y down, z towards the viewer) and strength, 0 = off
float3 SunDirection;
float SurfaceLight;
// ground variation (docs/GROUND_VARIATION_PLAN.md): 0 = off, 1 = full
float GroundVariation;

sampler DrawSampler : register(s0);
sampler HueSampler0 : register(s1);
sampler HueSampler1 : register(s2);
// normal map beside the drawn texture, same UV: rgb = normal, a = cavity (1 = open). Textures
// without one get a flat (128, 128, 255, 255) texel, which leaves the color unchanged.
sampler NormalSampler : register(s3);

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
	float2 WorldPos : TEXCOORD4;
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

// Soft shadow (docs/SHADOWS_PLAN.md): Hue.x = strength (> 0 marks a soft shadow), Normal =
// sprite bounds (u min, v min, u max), Hue.z = v max. Coverage is averaged over a disc that grows
// from the base (bottom of the sprite, at the feet) to the far end (top of the sprite). The quad
// is a few texels larger than the sprite; samples outside the sprite count as empty, since the
// atlas packs sprites without gaps.
float soft_shadow_tap(float2 uv, float2 lo, float2 hi)
{
	float2 inside = step(lo, uv) * step(uv, hi);

	return tex2Dlod(DrawSampler, float4(clamp(uv, lo, hi), 0, 0)).a * inside.x * inside.y;
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

// Water (docs/WATER_WEATHER_PLAN.md, W1). Pixels stay in place: the movement is a light pattern in
// world pixels. Hue.x = shore edge mask (1 NE, 2 NW, 4 SE, 8 SW: neighbour is not water), Normal.xy =
// position inside the 44x44 tile quad (0..1), used for the distance to the diamond edges.
float water_hash(float2 p)
{
	return frac(sin(dot(p, float2(12.9898f, 78.233f))) * 43758.5453f);
}

float water_edge(float mask, float bit, float distance)
{
	float on = step(bit, fmod(floor(mask / bit), 2.0f) * bit);

	return on * (1.0f - smoothstep(0.0f, 0.26f, distance));
}

float3 water(float3 color, PS_INPUT IN)
{
	float2 p = floor(IN.WorldPos);
	float t = Time;

	float w1 = sin(p.x * 0.045f + p.y * 0.090f + t * 1.1f);
	float w2 = sin(p.x * -0.070f + p.y * 0.050f + t * 1.7f + 1.3f);
	float w3 = sin((p.x + p.y) * 0.130f - t * 2.3f);

	// slow swell of the brightness
	color *= 1.0f + (w1 + w2) * 0.035f;

	// sparse glints where the waves meet
	float crest = w1 * 0.5f + w2 * 0.3f + w3 * 0.2f;
	float glint = smoothstep(0.86f, 1.0f, crest) * step(0.55f, water_hash(floor(p / 2.0f)));
	color += glint * 0.30f * AmbientTint;

	// shore foam along the edges whose neighbour is not water
	float mask = IN.Hue.x;

	if (mask > 0.5f)
	{
		float2 q = IN.Normal.xy - 0.5f;
		float foam = water_edge(mask, 1.0f, 0.5f - (q.x - q.y));
		foam = max(foam, water_edge(mask, 2.0f, 0.5f - (-q.x - q.y)));
		foam = max(foam, water_edge(mask, 4.0f, 0.5f - (q.x + q.y)));
		foam = max(foam, water_edge(mask, 8.0f, 0.5f - (q.y - q.x)));

		// broken into pixels that flicker, and breathing like small waves on the beach
		float breathe = 0.65f + 0.35f * sin(t * 1.4f + (p.x + p.y) * 0.08f);
		float grain = step(0.35f, water_hash(floor(p / 2.0f) + floor(t * 3.0f)));
		foam *= breathe * grain;

		color = lerp(color, float3(0.92f, 0.96f, 1.0f) * AmbientTint, foam * 0.55f);
	}

	return color;
}

// Brightness factor of the surface lighting: the sun on the relief (relative to a flat surface,
// which keeps its color) and a darker cavity in crevices
float surface_light(float2 uv)
{
	float4 texel = tex2D(NormalSampler, uv);
	float2 xy = (texel.xy * 255.0f - 128.0f) / 127.0f;
	float3 n = float3(xy, sqrt(saturate(1.0f - dot(xy, xy))));

	float relief = dot(n, SunDirection) - SunDirection.z;
	float cavity = 1.0f - texel.a;

	return 1.0f + SurfaceLight * (relief * 1.6f - cavity * 0.9f);
}

// smooth value noise, about one feature per unit
float ground_noise(float2 p)
{
	float2 i = floor(p);
	float2 f = frac(p);
	f = f * f * (3.0f - 2.0f * f);

	float a = water_hash(i);
	float b = water_hash(i + float2(1.0f, 0.0f));
	float c = water_hash(i + float2(0.0f, 1.0f));
	float d = water_hash(i + float2(1.0f, 1.0f));

	return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Ground variation: the same land texture repeats every tile, so patches of brightness, warmth and
// saturation over the world (one to ten tiles wide) break the grid. Stays put on the map (world pixels).
float3 ground_variation(float3 color, float2 world)
{
	float broad = ground_noise(world / 260.0f);
	float detail = ground_noise(world / 75.0f + 17.3f);
	float light = (broad * 0.6f + detail * 0.4f) * 2.0f - 1.0f;
	float warmth = ground_noise(world / 420.0f + 41.7f) * 2.0f - 1.0f;
	float saturation = ground_noise(world / 180.0f + 7.1f) * 2.0f - 1.0f;

	color *= 1.0f + light * 0.2f * GroundVariation;
	color *= 1.0f + float3(0.08f, 0.025f, -0.09f) * warmth * GroundVariation;

	float luma = dot(color, float3(0.2126f, 0.7152f, 0.0722f));
	color = lerp(luma.xxx, color, 1.0f + 0.18f * saturation * GroundVariation);

	return max(color, 0.0f);
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
	OUT.WorldPos = mul(position, WorldMatrix).xy + WorldOrigin;

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
	else if (mode == WATER)
	{
		color.rgb = water(color.rgb, IN);
	}

	if (GroundVariation > 0.0f && (mode == LAND || mode == LAND_COLOR || mode == LAND_FLAT || mode == NATURE_GRASS || mode == NATURE_GRASS_LIT))
	{
		color.rgb = ground_variation(color.rgb, IN.WorldPos);
	}

	if (SurfaceLight > 0.0f && (mode <= LAND_COLOR || mode == NATURE_GRASS || mode == NATURE_GRASS_LIT || mode == WATER || mode == LAND_FLAT)
		&& mode != HUE_TEXT_NO_BLACK && mode != HUE_TEXT)
	{
		color.rgb *= surface_light(IN.TexCoord.xy);
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

