// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Lighting
{
    internal enum LightFlickerType : byte
    {
        None,
        // fast, irregular dimming
        Fire,
        // slow pulse
        Magic
    }

    internal enum LightShape : byte
    {
        // the light.mul shape: keeps directional lights (wall torches, windows) lighting one side only
        Texture,
        // procedural round falloff on the quality levels that allow it
        Round
    }

    // How the enhanced lighting draws the light of one item graphic (docs/LIGHTING_PLAN.md, L8)
    internal readonly struct LightDefinition
    {
        public LightDefinition(float radius, float intensity, LightFlickerType flicker, LightShape shape)
        {
            Radius = radius;
            Intensity = intensity;
            Flicker = flicker;
            Shape = shape;
        }

        // size multiplier of the light shape
        public readonly float Radius;
        // 0..1, multiplies the light color
        public readonly float Intensity;
        public readonly LightFlickerType Flicker;
        public readonly LightShape Shape;
    }

    // Data/Client/lights_ex.txt: per item graphic light size, strength, flicker and shape for the
    // enhanced lighting. Upstream lights.txt (color) and lightshaders.txt (RGB + curve) are untouched.
    internal static class LightDefinitions
    {
        public const string FILE_NAME = "lights_ex.txt";
        public const float MAX_RADIUS = 4f;

        private static Dictionary<ushort, LightDefinition> _definitions = new Dictionary<ushort, LightDefinition>();

        public static int Count => _definitions.Count;

        public static bool TryGet(ushort graphic, out LightDefinition definition)
        {
            return _definitions.TryGetValue(graphic, out definition);
        }

        // Reads Data/Client/lights_ex.txt, writing the default file first when it does not exist
        public static void Load()
        {
            try
            {
                string path = Path.Combine(CUOEnviroment.ExecutablePath, "Data", "Client");

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                path = Path.Combine(path, FILE_NAME);

                if (!File.Exists(path))
                {
                    File.WriteAllText(path, DEFAULT_FILE);
                }

                _definitions = Parse(File.ReadAllText(path), message => Log.Warn($"{FILE_NAME}: {message}"));
            }
            catch (Exception ex)
            {
                Log.Error($"{FILE_NAME} could not be loaded: {ex.Message}");
                _definitions = Parse(DEFAULT_FILE, null);
            }
        }

        // Format: ITEM_ID[-LAST_ID] RADIUS INTENSITY FLICKER [SHAPE]. Bad lines are reported and skipped.
        public static Dictionary<ushort, LightDefinition> Parse(string text, Action<string> warn)
        {
            Dictionary<ushort, LightDefinition> result = new Dictionary<ushort, LightDefinition>();
            TextFileParser parser = new TextFileParser(text, new[] { ' ', '\t', ',' }, new[] { '#', ';' }, new[] { '"', '"' });

            while (!parser.IsEOF())
            {
                List<string> tokens = parser.ReadTokens();

                if (tokens == null || tokens.Count == 0)
                {
                    continue;
                }

                string line = string.Join(" ", tokens);

                if (
                    tokens.Count < 4
                    || !TryParseRange(tokens[0], out ushort first, out ushort last)
                    || !TryParseFloat(tokens[1], out float radius)
                    || !TryParseFloat(tokens[2], out float intensity)
                    || !Enum.TryParse(tokens[3], true, out LightFlickerType flicker)
                    || !Enum.IsDefined(flicker)
                )
                {
                    warn?.Invoke($"skipped bad line '{line}'");

                    continue;
                }

                LightShape shape = LightShape.Texture;

                if (tokens.Count > 4 && (!Enum.TryParse(tokens[4], true, out shape) || !Enum.IsDefined(shape)))
                {
                    warn?.Invoke($"skipped bad line '{line}'");

                    continue;
                }

                LightDefinition definition = new LightDefinition(
                    Math.Clamp(radius, 0.1f, MAX_RADIUS),
                    Math.Clamp(intensity, 0f, 1f),
                    flicker,
                    shape
                );

                for (int id = first; id <= last; id++)
                {
                    result[(ushort)id] = definition;
                }
            }

            return result;
        }

        private static bool TryParseRange(string token, out ushort first, out ushort last)
        {
            int dash = token.IndexOf('-');

            if (dash < 0)
            {
                bool ok = TryParseId(token, out first);
                last = first;

                return ok;
            }

            last = 0;

            return TryParseId(token.Substring(0, dash), out first)
                && TryParseId(token.Substring(dash + 1), out last)
                && first <= last;
        }

        private static bool TryParseId(string token, out ushort id)
        {
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
            }

            return ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }

        private static bool TryParseFloat(string token, out float value)
        {
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
        }

        // Graphics from the 7.0.x tiledata light sources. Wall mounted lights keep their light.mul
        // shape, which only lights the side in front of the wall.
        public const string DEFAULT_FILE =
@"# Enhanced lighting: how a light source item is drawn (docs/LIGHTING_PLAN.md, L8).
# Upstream lights.txt (color) and lightshaders.txt (RGB + curve) still choose the light color.
#
# FORMAT
# ITEM_ID[-LAST_ID] RADIUS INTENSITY FLICKER [SHAPE]
#   RADIUS     size multiplier of the light (1 = original size, max 4)
#   INTENSITY  0..1 strength of the light
#   FLICKER    none | fire | magic
#   SHAPE      texture (original light.mul shape, default) | round (soft round light)
# Delete this file to get the defaults back.

# candles
0x0A0F-0x0A11 0.9 0.9 fire round
0x0B1A-0x0B1C 0.9 0.9 fire round
0x142C-0x142E 0.9 0.9 fire round
0x1430-0x1432 0.9 0.9 fire round
0x1854-0x1856 0.9 0.8 fire round
0x1858-0x185A 0.9 0.8 fire round
0x236E-0x2373 0.9 0.9 fire round

# candelabras
0x0B1D-0x0B1F 1.0 1.0 fire round
0x0B26-0x0B28 1.1 1.0 fire round

# torches
0x0A12-0x0A14 1.1 1.0 fire round
0x088C 1.1 1.0 fire round

# wall torches and sconces
0x0A07-0x0A09 1.0 1.0 fire
0x0A0C-0x0A0E 1.0 1.0 fire
0x09FB 1.0 1.0 fire
0x09FD-0x09FF 1.0 1.0 fire
0x0A02-0x0A04 1.0 1.0 fire
0x3D94-0x3D96 1.0 1.0 fire

# lanterns and lamps
0x0A15-0x0A17 1.0 1.0 fire round
0x0A22-0x0A24 1.0 1.0 fire round
0x0A1A-0x0A1C 1.0 1.0 fire round
0x0B20 1.2 1.0 none round
0x0B22 1.2 1.0 none round
0x0B24 1.2 1.0 none round

# braziers, camp fires and fire pits
0x0E31-0x0E33 1.3 1.0 fire round
0x19AA 1.3 1.0 fire round
0x19BB 1.3 1.0 fire round
0x0DE3-0x0DE8 1.3 1.0 fire round
0x0DE9 1.0 0.7 fire round
0x0FAC 1.2 1.0 fire round
0xB2DB-0xB2E6 1.3 1.0 fire round

# fireplaces: directional light, original shape
0x0475-0x0476 1.0 1.0 fire
0x047B-0x0480 1.0 1.0 fire

# magic fields and gates
0x398C-0x399F 1.0 0.9 fire round
0x3946-0x3964 1.0 0.9 magic round
0x0F6C-0x0F70 1.2 1.0 magic round
0x1FD4-0x1FD8 1.2 1.0 magic round
0x0DDA-0x0DDE 1.2 1.0 magic round
0x0E2E-0x0E30 0.9 0.8 magic round
0x0E5C 0.8 0.8 magic round
0x0E5F 0.8 0.8 magic round
0x0E62 0.8 0.8 magic round
0x0E65 0.8 0.8 magic round
0x0E68 0.8 0.8 magic round
";
    }
}
