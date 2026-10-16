// SPDX-License-Identifier: BSD-2-Clause

using System.Globalization;

namespace ClassicUO.Game.Lighting
{
    // "lightdebug" command (docs/LIGHTING_PLAN.md, L13): lighting numbers in the FPS box and a
    // light map only view. The pass time is CPU time (Stopwatch): it does not measure the GPU.
    internal static class LightingDebug
    {
        // weight of the newest frame in the shown average
        private const double SMOOTHING = 0.1;

        private static bool _enhanced;
        private static int _lights, _maxLights, _lightMapWidth, _lightMapHeight;
        private static double _cpuMs;

        public static bool Enabled { get; set; }

        // world drawn white: only the light map (ambient + lights) is visible
        public static bool ShowLightMapOnly { get; set; }

        public static void RecordEnhanced(int lights, int maxLights, int lightMapWidth, int lightMapHeight, double cpuMs)
        {
            _enhanced = true;
            _lights = lights;
            _maxLights = maxLights;
            _lightMapWidth = lightMapWidth;
            _lightMapHeight = lightMapHeight;
            _cpuMs = _cpuMs <= 0 ? cpuMs : _cpuMs + (cpuMs - _cpuMs) * SMOOTHING;
        }

        public static void RecordLegacy()
        {
            _enhanced = false;
            _cpuMs = 0;
        }

        public static string GetText()
        {
            if (!_enhanced)
            {
                return "- Lighting: original path (day, alternative lights or enhanced off)\n";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "- Lighting: {0}/{1} lights, light map {2}x{3}{4}\n    - Light pass CPU {5:0.00} ms (not GPU time)\n",
                _lights,
                _maxLights,
                _lightMapWidth,
                _lightMapHeight,
                ShowLightMapOnly ? ", LIGHT MAP ONLY" : "",
                _cpuMs
            );
        }
    }
}
