// SPDX-License-Identifier: BSD-2-Clause

using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Lighting
{
    public enum SunShadowMode
    {
        // the original UO direction, always the same
        Classic = 0,
        // follows the time of day (SunClock)
        GameClock = 1,
        // a fixed hour picked in the options
        FixedHour = 2
    }

    // Direction and length of the sun shadow for a time of day (docs/SHADOWS_PLAN.md, S7).
    // The sun rises in the east at 6:00, is due south at noon and sets in the west at 18:00; at
    // night the moon follows the same arc (the shadows are faded by the ambient light anyway, this
    // only keeps a direction when a shard is bright at night). On the UO screen east is down right,
    // south down left, west up left and north up right: shadows fall up left in the morning, up
    // right at noon (the original UO shadow) and down right in the evening.
    internal static class SunPath
    {
        public const float SUNRISE = 6f * 60f;
        public const float DAY_LENGTH = 12f * 60f;
        // sun height at noon; noon shadows have the original UO length
        public const float NOON_ELEVATION = 62f * MathF.PI / 180f;
        // longest shadow, as a multiple of the original length (low sun)
        public const float MAX_LENGTH = 2f;
        // part of the arc (sin of the arc angle) over which the shadow fades in after sunrise
        public const float HORIZON_FADE = 0.2f;

        // the original shadow offset per half sprite height: up and to the right
        public static readonly Vector2 Classic = new Vector2(1f, -1f);

        // offset: screen offset of the shadow tip per half sprite height (Classic at noon).
        // Returns the strength factor (0 at the horizon, 1 once the sun is up).
        public static float Get(float minutes, out Vector2 offset)
        {
            float t = ((minutes - SUNRISE) % DAY_LENGTH + DAY_LENGTH) % DAY_LENGTH / DAY_LENGTH;
            float angle = t * MathF.PI;
            float height = MathF.Sin(angle);

            // map direction away from the sun (x east, y south), then to the screen: one map
            // step east is (+1, +1) on the screen, one step south (-1, +1)
            float mapX = -MathF.Cos(angle);
            float mapY = -height;
            Vector2 screen = new Vector2(mapX - mapY, mapX + mapY);

            float elevation = height * NOON_ELEVATION;
            float length = elevation <= 0.001f
                ? MAX_LENGTH
                : Math.Clamp(MathF.Tan(NOON_ELEVATION) / MathF.Tan(elevation), 1f, MAX_LENGTH);

            offset = screen * length;

            float fade = Math.Clamp(height / HORIZON_FADE, 0f, 1f);

            return fade * fade * (3f - 2f * fade);
        }
    }
}
