// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MathHelper = Microsoft.Xna.Framework.MathHelper;

namespace ClassicUO.Game
{
    // Enhanced weather (docs/WATER_WEATHER_PLAN.md, H1-H4): layered rain with ground splashes, soft
    // swaying snow, a darker/hazier sky while it lasts and lightning flashes in storms. The server
    // weather (0x65) stays authoritative: Weather decides what falls, how much and when it ends.
    internal sealed class WeatherRenderer
    {
        private const int MAX_DROPS = 900;
        private const int MAX_FLAKES = 450;
        private const int MAX_SPLASHES = 480;
        // particles per legacy 640x480 window at the server's full count (70)
        private const float RAIN_DENSITY = 330f;
        private const float SNOW_DENSITY = 200f;
        private const float LEGACY_AREA = 640f * 480f;
        // seconds to fade the weather in and out
        private const float FADE_SECONDS = 3f;

        private struct Particle
        {
            public Vector2 Position;
            // 0 far .. 1 near: size, speed and opacity
            public float Depth;
            public float Phase;
            // rain: screen y where the drop hits the ground
            public float GroundY;
        }

        private enum SplashKind : byte
        {
            Glint,
            Ripple,
            Droplet
        }

        private struct Splash
        {
            public SplashKind Kind;
            public Vector2 Position, Velocity;
            // age starts below 0 for splashes that appear a moment later
            public float Age, Life, Size, Alpha;
        }

        private readonly Particle[] _particles = new Particle[Math.Max(MAX_DROPS, MAX_FLAKES)];
        private readonly Splash[] _splashes = new Splash[MAX_SPLASHES];
        private int _particleCount, _splashCount;
        private bool _snow;
        private float _intensity;
        private float _flashTime = -1f, _flashStrength;
        private Texture2D _white, _dot, _ring, _streak;

        // 0..1 lightning brightness this frame; the enhanced lighting adds it to the ambient light
        public static float Flash { get; private set; }

        // particles drawn by the last Draw, for the -lightdebug overlay
        public static int LastParticleCount { get; private set; }

        public float Intensity => _intensity;

        public void Reset()
        {
            _particleCount = 0;
            _splashCount = 0;
            _intensity = 0f;
            _flashTime = -1f;
            Flash = 0f;
            LastParticleCount = 0;
        }

        // A thunder sound was played: flash with it
        public void TriggerLightning()
        {
            _flashTime = 0f;
            _flashStrength = RandomHelper.GetValue(60, 100) / 100f;
        }

        // type: server weather, count: server particle count (0..70), active: false once the server
        // weather ran out (fades out). x/y: world pixels of the view's top left corner, scale: world
        // pixels per screen pixel, width/height: screen pixels of the view. isWater: is the ground at
        // this world pixel water (rain rings there, splashes on land); null means land.
        public void Draw(
            UltimaBatcher2D batcher,
            WeatherType type,
            int count,
            sbyte wind,
            bool active,
            bool lightning,
            Vector2 viewOrigin,
            float scale,
            int width,
            int height,
            float layerDepth,
            float delta,
            Func<Vector2, bool> isWater = null
        )
        {
            delta = Math.Clamp(delta, 0f, 0.1f);

            bool snow = type == WeatherType.WT_SNOW;
            bool storm = type == WeatherType.WT_STORM_APPROACH || type == WeatherType.WT_STORM_BREWING;

            if (snow != _snow)
            {
                _snow = snow;
                _particleCount = 0;
                _splashCount = 0;
            }

            float target = active ? Math.Clamp(count / 70f, 0f, 1f) : 0f;
            float step = delta / FADE_SECONDS;
            _intensity = _intensity < target ? Math.Min(target, _intensity + step) : Math.Max(target, _intensity - step);

            float area = width * (float)height / LEGACY_AREA;
            int wanted = (int)(_intensity * area * (snow ? SNOW_DENSITY : RAIN_DENSITY));
            wanted = Math.Min(wanted, snow ? MAX_FLAKES : MAX_DROPS);

            while (_particleCount < wanted)
            {
                Spawn(ref _particles[_particleCount++], width, height, true);
            }

            _particleCount = Math.Min(_particleCount, wanted);

            EnsureTextures(batcher.GraphicsDevice);

            float time = Time.Ticks / 1000f;

            // the sky: rain and storms darken and cool the scene a little, snow adds a white haze
            if (_intensity > 0f)
            {
                Rectangle view = new Rectangle((int)viewOrigin.X, (int)viewOrigin.Y, (int)(width * scale) + 1, (int)(height * scale) + 1);

                if (snow)
                {
                    batcher.Draw(_white, view, new Vector3(0, 0, 0.08f * _intensity), layerDepth);
                }
                else
                {
                    batcher.Draw(SolidColorTextureCache.GetTexture(new Color(30, 40, 55)), view, new Vector3(0, 0, (storm ? 0.24f : 0.14f) * _intensity), layerDepth);
                }
            }

            // streaks, rings and flakes are scaled down soft textures: filter them instead of the
            // world's point sampling, which would break thin rings into dashes
            batcher.SetSampler(SamplerState.LinearClamp);

            if (snow)
            {
                DrawSnow(batcher, wind, viewOrigin, scale, width, height, layerDepth, delta, time);
            }
            else
            {
                DrawRain(batcher, wind, storm, viewOrigin, scale, width, height, layerDepth, delta, time, isWater);
            }

            batcher.SetSampler(SamplerState.PointClamp);

            DrawLightning(batcher, storm && lightning, viewOrigin, scale, width, height, layerDepth, delta);

            LastParticleCount = _particleCount + _splashCount;
        }

        private void DrawRain(UltimaBatcher2D batcher, sbyte wind, bool storm, Vector2 origin, float scale, int width, int height, float layerDepth, float delta, float time, Func<Vector2, bool> isWater)
        {
            // slow gusts sway the slant a little so the rain never looks like a fixed pattern
            float gust = (float)Math.Sin(time * 0.37f) * 0.6f + (float)Math.Sin(time * 0.91f + 1.3f) * 0.4f;
            float slant = -0.16f + wind * (storm ? 0.08f : 0.05f) + gust * (storm ? 0.09f : 0.045f);
            float fade = Math.Min(1f, _intensity * 2f);

            // the player stands in the middle of the view: near drops thin out there so the
            // character and what is around it stay readable
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float clearRadius = Math.Max(1f, Math.Min(width, height) * 0.24f);

            float streakW = _streak.Width;
            float streakH = _streak.Height;
            Vector2 pivot = new Vector2(0f, streakH * 0.5f);

            for (int i = 0; i < _particleCount; i++)
            {
                ref Particle p = ref _particles[i];

                float speed = MathHelper.Lerp(640f, 1180f, p.Depth) * (storm ? 1.2f : 1f);
                Vector2 velocity = new Vector2(slant * speed, speed);
                p.Position += velocity * delta;

                if (p.Position.Y >= p.GroundY)
                {
                    // far drops vanish in the distance, the rest leave a mark where they land
                    if (p.Depth > 0.25f)
                    {
                        Vector2 at = new Vector2(p.Position.X, p.GroundY);
                        bool water = isWater != null && isWater(origin + at * scale);

                        if (water)
                        {
                            WaterImpact(at, p.Depth);
                        }
                        else
                        {
                            LandImpact(at, p.Depth);
                        }
                    }

                    Spawn(ref p, width, height, false);

                    continue;
                }

                if (p.Position.X < -60f)
                {
                    p.Position.X += width + 120f;
                }
                else if (p.Position.X > width + 60f)
                {
                    p.Position.X -= width + 120f;
                }

                float length = MathHelper.Lerp(10f, 42f, p.Depth * p.Depth);
                float stroke = p.Depth > 0.8f ? 2f : p.Depth > 0.5f ? 1.5f : 1f;
                float alpha = MathHelper.Lerp(0.35f, 1f, p.Depth) * fade;

                if (p.Depth > 0.45f)
                {
                    float d = Vector2.Distance(p.Position, center) / clearRadius;
                    alpha *= MathHelper.Lerp(0.25f, 1f, SmoothStep(0.45f, 1.25f, d));
                }

                Vector2 dir = Vector2.Normalize(velocity);
                Vector2 tail = p.Position - dir * length;

                batcher.Draw(
                    _streak,
                    origin + tail * scale,
                    null,
                    new Vector3(0, 0, alpha),
                    (float)Math.Atan2(dir.Y, dir.X),
                    pivot,
                    new Vector2(length / streakW, stroke / streakH) * scale,
                    SpriteEffects.None,
                    layerDepth
                );
            }

            for (int i = 0; i < _splashCount; i++)
            {
                ref Splash s = ref _splashes[i];
                s.Age += delta;

                if (s.Age >= s.Life)
                {
                    _splashes[i--] = _splashes[--_splashCount];

                    continue;
                }

                if (s.Age < 0f)
                {
                    continue;
                }

                float t = s.Age / s.Life;
                Vector2 at = origin + s.Position * scale;

                switch (s.Kind)
                {
                    case SplashKind.Ripple:
                    {
                        // a flat ellipse (the ground seen from the UO camera) that widens and fades
                        float grow = 1f - (1f - t) * (1f - t);
                        float w = s.Size * (0.25f + 0.75f * grow) * scale;
                        float h = w * 0.42f;

                        batcher.Draw(
                            _ring,
                            new Rectangle((int)(at.X - w * 0.5f), (int)(at.Y - h * 0.5f), Math.Max(2, (int)w), Math.Max(1, (int)h)),
                            new Vector3(0, 0, s.Alpha * (1f - t) * (1f - t) * fade),
                            layerDepth
                        );

                        break;
                    }

                    case SplashKind.Droplet:
                    {
                        // tiny crown droplets thrown up by the impact, falling back
                        s.Position += s.Velocity * delta;
                        s.Velocity.Y += 900f * delta;
                        int size = Math.Max(1, (int)Math.Round(2f * scale));

                        batcher.Draw(
                            _dot,
                            new Rectangle((int)(at.X - size * 0.5f), (int)(at.Y - size * 0.5f), size, size),
                            new Vector3(0, 0, s.Alpha * (1f - t) * fade),
                            layerDepth
                        );

                        break;
                    }

                    default:
                    {
                        // the bright point where the drop hits
                        float size = s.Size * (1f - 0.5f * t) * scale;

                        batcher.Draw(
                            _dot,
                            new Rectangle((int)(at.X - size * 0.5f), (int)(at.Y - size * 0.35f), Math.Max(1, (int)size), Math.Max(1, (int)(size * 0.7f))),
                            new Vector3(0, 0, s.Alpha * (1f - t) * fade),
                            layerDepth
                        );

                        break;
                    }
                }
            }
        }

        // water: no spray, wide rings that spread out slowly one after the other, and for near drops
        // a single droplet jumping straight up from the centre
        private void WaterImpact(Vector2 position, float depth)
        {
            AddSplash(SplashKind.Glint, position, Vector2.Zero, 0f, 0.06f, MathHelper.Lerp(2f, 4f, depth), 0.6f);
            AddSplash(SplashKind.Ripple, position, Vector2.Zero, 0f, MathHelper.Lerp(0.7f, 1.05f, depth), MathHelper.Lerp(18f, 36f, depth), MathHelper.Lerp(0.55f, 0.85f, depth));
            AddSplash(SplashKind.Ripple, position, Vector2.Zero, -0.14f, MathHelper.Lerp(0.6f, 0.9f, depth), MathHelper.Lerp(11f, 22f, depth), MathHelper.Lerp(0.4f, 0.65f, depth));

            if (depth > 0.65f)
            {
                AddSplash(SplashKind.Ripple, position, Vector2.Zero, -0.3f, 0.6f, MathHelper.Lerp(7f, 13f, depth), 0.45f);
                AddSplash(SplashKind.Droplet, position, new Vector2(0f, -RandomHelper.GetValue(80, 120) * depth), 0f, 0.22f, 0f, 0.7f);
            }
        }

        // land: a quick bright splash with crown droplets thrown out, and only a small, short wet ring
        private void LandImpact(Vector2 position, float depth)
        {
            AddSplash(SplashKind.Glint, position, Vector2.Zero, 0f, 0.08f, MathHelper.Lerp(3f, 6f, depth), 0.85f);
            AddSplash(SplashKind.Ripple, position, Vector2.Zero, 0f, MathHelper.Lerp(0.2f, 0.3f, depth), MathHelper.Lerp(7f, 13f, depth), MathHelper.Lerp(0.45f, 0.7f, depth));

            if (depth > 0.5f)
            {
                int drops = RandomHelper.GetValue(3, 4);

                for (int i = 0; i < drops; i++)
                {
                    Vector2 velocity = new Vector2(RandomHelper.GetValue(-70, 70) * depth, -RandomHelper.GetValue(90, 160) * depth);
                    AddSplash(SplashKind.Droplet, position, velocity, 0f, 0.3f, 0f, 0.75f);
                }
            }
        }

        private void AddSplash(SplashKind kind, Vector2 position, Vector2 velocity, float age, float life, float size, float alpha)
        {
            if (_splashCount >= MAX_SPLASHES)
            {
                return;
            }

            _splashes[_splashCount++] = new Splash
            {
                Kind = kind,
                Position = position,
                Velocity = velocity,
                Age = age,
                Life = life,
                Size = size,
                Alpha = alpha
            };
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);

            return t * t * (3f - 2f * t);
        }


        private void DrawSnow(UltimaBatcher2D batcher, sbyte wind, Vector2 origin, float scale, int width, int height, float layerDepth, float delta, float time)
        {
            _splashCount = 0;

            for (int i = 0; i < _particleCount; i++)
            {
                ref Particle p = ref _particles[i];

                float fall = MathHelper.Lerp(22f, 60f, p.Depth);
                float sway = (float)Math.Sin(time * MathHelper.Lerp(0.8f, 1.6f, p.Phase) + p.Phase * 6.28f) * MathHelper.Lerp(8f, 22f, p.Depth);
                p.Position += new Vector2((wind * 9f + sway) * delta, fall * delta);

                if (p.Position.Y > height + 10f)
                {
                    Spawn(ref p, width, height, false);
                }

                if (p.Position.X < -20f)
                {
                    p.Position.X += width + 40f;
                }
                else if (p.Position.X > width + 20f)
                {
                    p.Position.X -= width + 40f;
                }

                float size = MathHelper.Lerp(2f, 5f, p.Depth) * scale;
                Vector2 center = origin + p.Position * scale;
                float alpha = MathHelper.Lerp(0.45f, 0.9f, p.Depth) * Math.Min(1f, _intensity * 2f);

                batcher.Draw(
                    _dot,
                    new Rectangle((int)(center.X - size * 0.5f), (int)(center.Y - size * 0.5f), Math.Max(1, (int)size), Math.Max(1, (int)size)),
                    new Vector3(0, 0, alpha),
                    layerDepth
                );
            }
        }

        // a quick double flash, then a fading glow
        private void DrawLightning(UltimaBatcher2D batcher, bool enabled, Vector2 origin, float scale, int width, int height, float layerDepth, float delta)
        {
            Flash = 0f;

            if (_flashTime < 0f)
            {
                return;
            }

            _flashTime += delta;

            if (!enabled || _flashTime > 0.8f)
            {
                _flashTime = -1f;

                return;
            }

            float t = _flashTime;
            float flash = t < 0.06f ? 1f : t < 0.12f ? 0.25f : t < 0.2f ? 0.8f : 0.8f * (1f - (t - 0.2f) / 0.6f);
            Flash = Math.Clamp(flash * _flashStrength * Math.Max(_intensity, 0.3f), 0f, 1f);

            Rectangle view = new Rectangle((int)origin.X, (int)origin.Y, (int)(width * scale) + 1, (int)(height * scale) + 1);
            batcher.Draw(_white, view, new Vector3(0, 0, 0.3f * Flash), layerDepth);
        }

        private static void Spawn(ref Particle p, int width, int height, bool anywhere)
        {
            p.Depth = RandomHelper.GetValue(0, 1000) / 1000f;
            p.Phase = RandomHelper.GetValue(0, 1000) / 1000f;
            p.Position.X = RandomHelper.GetValue(-40, width + 40);
            p.Position.Y = anywhere ? RandomHelper.GetValue(-20, height) : RandomHelper.GetValue(-60, -5);
            // nearer drops land lower on the screen
            p.GroundY = MathHelper.Lerp(height * 0.15f, height * 1.05f, (p.Depth + RandomHelper.GetValue(0, 1000) / 1000f) * 0.5f);

            if (p.Position.Y > p.GroundY)
            {
                p.Position.Y = p.GroundY * RandomHelper.GetValue(0, 1000) / 1000f;
            }
        }

        private void EnsureTextures(GraphicsDevice device)
        {
            if (_white == null || _white.IsDisposed)
            {
                _white = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
                _white.SetData(new[] { Color.White });
            }

            if (_dot == null || _dot.IsDisposed)
            {
                _dot = CreateRadial(device, 16, 16, false);
            }

            if (_ring == null || _ring.IsDisposed)
            {
                _ring = CreateRadial(device, 48, 24, true);
            }

            if (_streak == null || _streak.IsDisposed)
            {
                _streak = CreateStreak(device, 64, 5);
            }
        }

        // premultiplied white: a soft dot, or a thin ellipse ring for splashes
        private static Texture2D CreateRadial(GraphicsDevice device, int width, int height, bool ring)
        {
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f) / width * 2f - 1f;
                    float dy = (y + 0.5f) / height * 2f - 1f;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float a = ring
                        ? Math.Max(0f, 1f - Math.Abs(d - 0.8f) / 0.2f)
                        : Math.Clamp(1f - d, 0f, 1f);

                    byte v = (byte)(a * 255f + 0.5f);
                    pixels[y * width + x] = new Color(v, v, v, v);
                }
            }

            Texture2D texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
            texture.SetData(pixels);

            return texture;
        }

        // premultiplied, slightly cool white rain streak along +x: transparent tail, bright soft head
        private static Texture2D CreateStreak(GraphicsDevice device, int width, int height)
        {
            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height * 2f - 1f;
                float across = (float)Math.Exp(-v * v * 2.5f);

                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float along = (0.15f + 0.85f * (float)Math.Pow(u, 0.6f)) * SmoothStep(0f, 0.12f, u) * SmoothStep(1f, 0.9f, u);
                    float a = along * across;

                    pixels[y * width + x] = new Color((byte)(a * 215f), (byte)(a * 228f), (byte)(a * 245f), (byte)(a * 255f));
                }
            }

            Texture2D texture = new Texture2D(device, width, height, false, SurfaceFormat.Color);
            texture.SetData(pixels);

            return texture;
        }
    }
}
