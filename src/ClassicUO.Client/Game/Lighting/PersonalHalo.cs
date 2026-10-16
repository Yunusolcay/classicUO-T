// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Game.Lighting
{
    // Soft light around the player for the personal light level (0x4E), in world render target
    // pixels. Intensity 0 draws nothing.
    internal readonly struct PersonalHalo
    {
        public PersonalHalo(int x, int y, int diameter, float intensity)
        {
            X = x;
            Y = y;
            Diameter = diameter;
            Intensity = intensity;
        }

        public readonly int X, Y, Diameter;
        public readonly float Intensity;
    }
}
