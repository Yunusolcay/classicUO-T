// SPDX-License-Identifier: BSD-2-Clause

using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Lighting
{
    // How LightManager draws the frame's lights. The default value is the original drawing.
    internal readonly struct LightDrawOptions
    {
        public LightDrawOptions(bool useDefinitions, bool flicker, Texture2D roundTexture, uint ticks)
        {
            UseDefinitions = useDefinitions;
            Flicker = flicker;
            RoundTexture = roundTexture;
            Ticks = ticks;
        }

        // apply lights_ex.txt (enhanced lighting)
        public readonly bool UseDefinitions;
        public readonly bool Flicker;
        // soft round light for "round" definitions, null on quality levels without procedural falloff
        public readonly Texture2D RoundTexture;
        public readonly uint Ticks;
    }
}
