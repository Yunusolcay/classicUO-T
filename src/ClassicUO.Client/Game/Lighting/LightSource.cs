// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Game.Lighting
{
    // A light collected for the current frame, in world render target pixels
    internal struct LightSource
    {
        public byte ID;
        public ushort Color;
        public bool IsHue;
        public int DrawX,
            DrawY;

        // lights_ex.txt entry of the light item, used by the enhanced lighting only
        public bool HasDefinition;
        public LightDefinition Definition;
        // stable per light source: the flicker of two torches side by side does not line up
        public int Seed;
    }
}
