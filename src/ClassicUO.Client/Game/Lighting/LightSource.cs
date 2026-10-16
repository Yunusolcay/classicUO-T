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
    }
}
