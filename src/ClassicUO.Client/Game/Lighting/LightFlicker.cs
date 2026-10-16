// SPDX-License-Identifier: BSD-2-Clause

using System;

namespace ClassicUO.Game.Lighting
{
    // Time based light flicker (docs/LIGHTING_PLAN.md, L9): the same at any FPS, phase from a
    // per light seed. Fire dims irregularly and fast, magic pulses slowly.
    internal static class LightFlicker
    {
        private const float FIRE_DIM = 0.18f;
        private const float FIRE_GROW = 0.03f;
        private const float MAGIC_DIM = 0.12f;
        private const float MAGIC_GROW = 0.02f;

        // intensity: multiplier in 0..1, scale: size multiplier around 1
        public static void Sample(LightFlickerType type, uint ticks, int seed, out float intensity, out float scale)
        {
            // double: Time.Ticks grows for the whole session and float sines lose precision
            double t = ticks / 1000.0;
            double phase = GetPhase(seed);
            double wave;
            float dim, grow;

            switch (type)
            {
                case LightFlickerType.Fire:
                    wave = 0.55 * Math.Sin(t * 7.1 + phase)
                        + 0.30 * Math.Sin(t * 12.7 + phase * 1.7)
                        + 0.15 * Math.Sin(t * 23.3 + phase * 2.9);
                    dim = FIRE_DIM;
                    grow = FIRE_GROW;

                    break;

                case LightFlickerType.Magic:
                    wave = Math.Sin(t * 2.2 + phase);
                    dim = MAGIC_DIM;
                    grow = MAGIC_GROW;

                    break;

                default:
                    intensity = 1f;
                    scale = 1f;

                    return;
            }

            intensity = 1f - dim * (0.5f + 0.5f * (float)wave);
            scale = 1f + grow * (float)wave;
        }

        private static double GetPhase(int seed)
        {
            uint hash = unchecked((uint)seed * 2654435761u);

            return (hash >> 8) / 16777216.0 * Math.PI * 2.0;
        }
    }
}
