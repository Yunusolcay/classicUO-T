// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Game.Lighting
{
    // Soft shadow tuning (docs/SHADOWS_PLAN.md, S1)
    internal static class ShadowSettings
    {
        public const bool DEFAULT_SOFT_SHADOWS = true;

        // darkness of a full shadow in daylight: the original shadows use 0.4
        public const float DAY_STRENGTH = 0.4f;
        // ambient level (IsometricLevel) where the sun shadows start to fade, and where they are gone
        public const float FADE_START = 0.85f;
        public const float FADE_END = 0.35f;

        // The shadows come from the sun: they fade out as the ambient light goes down at night
        public static float GetStrength(float ambientLevel)
        {
            float t = (ambientLevel - FADE_END) / (FADE_START - FADE_END);

            if (t <= 0f)
            {
                return 0f;
            }

            if (t >= 1f)
            {
                return DAY_STRENGTH;
            }

            return DAY_STRENGTH * t * t * (3f - 2f * t);
        }
    }
}
