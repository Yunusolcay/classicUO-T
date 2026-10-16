// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Lighting
{
    // A colored round light for the enhanced light map, in world render target pixels
    internal struct EffectLight
    {
        public Vector2 Position;
        public float Radius;
        // RGB 0..1, intensity already applied
        public Vector3 Color;
    }

    // Lights of spell and item effects (docs/EFFECTS_PLAN.md, E1/E3): every bright effect lights its
    // surroundings in its own color, and a projectile hitting its target leaves a short flash. Filled
    // while the world is drawn, drawn into the light map by LightMapRenderer.
    internal static class EffectLights
    {
        public const int MAX_LIGHTS = 128;
        private const float BURST_SECONDS = 0.35f;
        // effects darker than this (luma of their average color) do not light anything
        private const float MIN_LUMA = 0.25f;

        private struct Burst
        {
            // world pixels, not relative to the camera: the camera may move during the flash
            public Vector2 Absolute;
            public Vector3 Color;
            public float Radius;
            public uint Start;
        }

        private static readonly EffectLight[] _lights = new EffectLight[MAX_LIGHTS];
        private static readonly List<Burst> _bursts = new List<Burst>();
        private static readonly Dictionary<ushort, Vector3> _colors = new Dictionary<ushort, Vector3>();
        private static int _count;

        public static int Count => _count;

        public static ReadOnlySpan<EffectLight> Lights => new ReadOnlySpan<EffectLight>(_lights, 0, _count);

        public static void Add(Vector2 position, float radius, Vector3 color)
        {
            if (_count < MAX_LIGHTS)
            {
                _lights[_count++] = new EffectLight { Position = position, Radius = radius, Color = color };
            }
        }

        // A projectile hit at a world pixel: a quick flash of its color there
        public static void AddBurst(Vector2 absolute, Vector3 color, float radius)
        {
            if (_bursts.Count < MAX_LIGHTS / 2)
            {
                _bursts.Add(new Burst { Absolute = absolute, Color = color, Radius = radius, Start = Time.Ticks });
            }
        }

        // Adds the running flashes to this frame's lights; drawOffset: world pixel of the target origin
        public static void AddBursts(Point drawOffset)
        {
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                Burst burst = _bursts[i];
                float t = (Time.Ticks - burst.Start) / (BURST_SECONDS * 1000f);

                if (t >= 1f)
                {
                    _bursts.RemoveAt(i);

                    continue;
                }

                // fast rise, slower fall
                float strength = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;

                Add(
                    burst.Absolute - new Vector2(drawOffset.X, drawOffset.Y),
                    burst.Radius * (0.7f + 0.5f * t),
                    burst.Color * strength
                );
            }
        }

        public static void Clear()
        {
            _count = 0;
        }

        // Average color of an effect frame's art, its brightness taken out (largest channel 1).
        // Zero for dark art. Cached per graphic.
        public static Vector3 GetColor(ushort graphic)
        {
            if (_colors.TryGetValue(graphic, out Vector3 color))
            {
                return color;
            }

            color = Vector3.Zero;

            try
            {
                var art = Client.Game.UO.FileManager.Arts.GetArt((uint)(graphic + 0x4000));
                color = Average(art.Pixels);
            }
            catch (Exception)
            {
                // missing art: no light
            }

            _colors[graphic] = color;

            return color;
        }

        // pixels: 0xAABBGGRR; transparent pixels are skipped, bright pixels count more
        public static Vector3 Average(ReadOnlySpan<uint> pixels)
        {
            Vector3 sum = Vector3.Zero;
            float weight = 0f;

            foreach (uint p in pixels)
            {
                if ((p >> 24) == 0)
                {
                    continue;
                }

                Vector3 c = new Vector3(p & 0xFF, (p >> 8) & 0xFF, (p >> 16) & 0xFF) / 255f;
                float luma = c.X * 0.2126f + c.Y * 0.7152f + c.Z * 0.0722f;
                sum += c * luma;
                weight += luma;
            }

            if (weight <= 0f)
            {
                return Vector3.Zero;
            }

            Vector3 average = sum / weight;

            if (average.X * 0.2126f + average.Y * 0.7152f + average.Z * 0.0722f < MIN_LUMA)
            {
                return Vector3.Zero;
            }

            float max = Math.Max(average.X, Math.Max(average.Y, average.Z));

            return average / max;
        }
    }
}
