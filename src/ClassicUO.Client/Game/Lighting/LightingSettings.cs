// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Game.Lighting
{
    internal enum LightingQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    // Profile defaults and preset data only; rendering opts in separately.
    internal static class LightingSettings
    {
        public const bool DEFAULT_ENHANCED_LIGHTING = true;
        public const LightingQuality DEFAULT_QUALITY = LightingQuality.Medium;
        public const bool DEFAULT_LIGHT_FLICKER = true;
        public const bool DEFAULT_DAY_NIGHT_TINT = true;
        public const float DEFAULT_LIGHT_TRANSITION_SECONDS = 2.0f;
        public const float DEFAULT_LIGHT_INTENSITY = 1.0f;

        public static LightingQualityPreset GetQualityPreset(LightingQuality quality)
        {
            return quality switch
            {
                LightingQuality.Low => new LightingQualityPreset(
                    lightMapScale: 0.25f, blurPasses: 0, blurTaps: 0,
                    maxLights: 128, useProceduralFalloff: false),
                LightingQuality.High => new LightingQualityPreset(
                    lightMapScale: 0.5f, blurPasses: 2, blurTaps: 9,
                    maxLights: 512, useProceduralFalloff: true),
                // Unknown values in a hand-edited profile use the default preset.
                _ => new LightingQualityPreset(
                    lightMapScale: 0.5f, blurPasses: 0, blurTaps: 0,
                    maxLights: 256, useProceduralFalloff: true)
            };
        }
    }

    internal readonly struct LightingQualityPreset
    {
        public LightingQualityPreset(
            float lightMapScale, int blurPasses, int blurTaps,
            int maxLights, bool useProceduralFalloff)
        {
            LightMapScale = lightMapScale;
            BlurPasses = blurPasses;
            BlurTaps = blurTaps;
            MaxLights = maxLights;
            UseProceduralFalloff = useProceduralFalloff;
        }

        public float LightMapScale { get; }
        public int BlurPasses { get; }
        public int BlurTaps { get; }
        public int MaxLights { get; }
        public bool UseProceduralFalloff { get; }
    }
}
