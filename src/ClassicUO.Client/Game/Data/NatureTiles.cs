// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Data
{
    // Client-only "nature effects" (Profile.NatureEffects): livelier grass colors and wind sway.
    // Purely visual: nothing is sent to the server, no game object changes, and sway never moves
    // anything far enough to hide or reveal what other players see.
    internal static class NatureTiles
    {
        // Sway amplitude per pixel of sprite height. Using the same shear for every sprite keeps
        // multi-part objects together: a tree trunk and its separate leaves sprite shift by the same
        // amount at any given screen row.
        private const float GRASS_SHEAR = 0.05f;
        private const float TREE_SHEAR = 0.022f;
        private const float MAX_SWAY_PX = 3f;

        private static bool[] _greenLand;
        private static bool[] _tuftLand;
        private static byte[] _staticKind; // 0 none, 1 low plant, 2 tree / leaves

        private const byte KIND_PLANT = 1;
        private const byte KIND_TREE = 2;

        public static bool Enabled => ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.NatureEffects;

        public static bool IsGreenLand(ushort graphic)
        {
            EnsureLoaded();

            return graphic < _greenLand.Length && _greenLand[graphic];
        }

        // Wind sway for a static sprite of the given height: (pixels at the top, phase) or zero.
        public static Vector2 GetSway(ushort graphic, int x, int y, int spriteHeight)
        {
            EnsureLoaded();

            byte kind = graphic < _staticKind.Length ? _staticKind[graphic] : (byte)0;

            if (kind == 0 || spriteHeight <= 0)
            {
                return Vector2.Zero;
            }

            float amplitude = Math.Min(MAX_SWAY_PX, spriteHeight * (kind == KIND_TREE ? TREE_SHEAR : GRASS_SHEAR));

            return new Vector2(amplitude, GetPhase(x, y));
        }

        public static bool IsPlantOrTree(ushort graphic)
        {
            EnsureLoaded();

            return graphic < _staticKind.Length && _staticKind[graphic] != 0;
        }

        // Diagonal wave across the map, with a little per-tile jitter so neighbours are not in lockstep.
        public static float GetPhase(int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);

            return x * 0.55f + y * 0.35f + (h & 0xFF) / 255f * 0.8f;
        }

        // Low-frequency tint noise in [-1, 1] from world tile coordinates, used to vary grass color
        // across meadows. Vertices of neighbouring tiles share corners, so there are no seams.
        public static float GetTint(float worldX, float worldY)
        {
            return (float)(Math.Sin(worldX * 0.13 + worldY * 0.05) * Math.Sin(worldY * 0.11 - worldX * 0.07 + 1.3));
        }

        // Grass shader mode for an unhued tile; hued tiles keep their own hue.
        public static byte GrassMode(bool lit) => lit ? ShaderHueTranslator.SHADER_NATURE_GRASS_LIT : ShaderHueTranslator.SHADER_NATURE_GRASS;

        private static void EnsureLoaded()
        {
            if (_greenLand != null)
            {
                return;
            }

            var tiledata = Client.Game.UO.FileManager.TileData;
            LandTiles[] land = tiledata.LandData;
            StaticTiles[] statics = tiledata.StaticData;

            var greenLand = new bool[land.Length];
            var tuftLand = new bool[land.Length];

            for (int i = 0; i < land.Length; i++)
            {
                string name = land[i].Name;

                // includes the "decimated_" transition tiles; the shader only touches green pixels
                greenLand[i] = !string.IsNullOrEmpty(name)
                    && (Contains(name, "grass") || Contains(name, "forest") || Contains(name, "jungle"));
                // tufts only on full grass tiles, not on grass/dirt transitions
                tuftLand[i] = greenLand[i] && !Contains(name, "decimated");
            }

            var kinds = new byte[statics.Length];

            for (int i = 0; i < statics.Length; i++)
            {
                kinds[i] = Classify((ushort)i, ref statics[i]);
            }

            _staticKind = kinds;
            _tuftLand = tuftLand;
            _greenLand = greenLand;
        }

        // Whole-word whitelist: substring matching swayed "divine fury", "book of bushido",
        // "abyssal infernal", floor mosaics, hair items and wall vines.
        private static readonly HashSet<string> _treeWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "tree", "trees", "leaves", "foliage"
        };

        private static readonly HashSet<string> _plantWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "grass", "grasses", "fern", "ferns", "flowers", "weed", "weeds", "reeds", "cattails", "bulrushes",
            "wheat", "bush", "shrub", "shrubs"
        };

        // decorations, furniture, crafted items and anything attached to walls or ships
        private static readonly HashSet<string> _excludedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "sail", "sails", "ship", "pot", "flowerpot", "bed", "flowerbed", "mat", "tile", "dried", "bushel",
            "sheaf", "sheaves", "wall", "stump", "log", "logs", "vase", "urn", "painting", "deed", "seed", "seeds",
            "mosaic", "xmas", "christmas", "holiday", "pillow", "bonsai", "hair", "coral", "frame", "fram",
            "ornament", "ornam", "ribon", "ribbon", "light", "lights", "angel", "snowflake", "artisan", "garland",
            "bowl", "clippings", "planter", "pedestal", "pedes", "statue", "softened", "partialhue", "decorative",
            "bark", "mini", "potted", "pumpkin", "shea"
        };

        internal static byte Classify(ushort graphic, ref StaticTiles data)
        {
            string name = data.Name;

            if (string.IsNullOrEmpty(name) || data.IsInternal || data.IsAnimated || data.IsWet)
            {
                return 0;
            }

            // hair styles are named like "tree _hair02_blk_mu"
            if (name.Contains("hair", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            string[] words = name.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
            bool tree = false, plant = false;

            foreach (string word in words)
            {
                if (_excludedWords.Contains(word))
                {
                    return 0;
                }

                tree |= _treeWords.Contains(word);
                plant |= _plantWords.Contains(word);
            }

            if (tree)
            {
                return KIND_TREE;
            }

            // "tree" tiles that the stump/tree filter knows but are named differently
            if (data.IsFoliage && StaticFilters.IsTree(graphic, out _))
            {
                return KIND_TREE;
            }

            return plant ? KIND_PLANT : (byte)0;
        }

        private static readonly char[] _separators = { ' ', '_', '-', '%', '/', '\\', '.', ',', '\'' };

        // ---- grass tufts --------------------------------------------------------------------
        // Small procedurally drawn grass tufts placed on grass tiles. Their position comes from a
        // hash of the tile coordinates, so they appear at the same spots every time.
        // To never hide anything from the player they stay inside the lower half of their own tile
        // and are drawn just above the ground: items, corpses, mobiles and statics always cover them.

        public const int TUFT_WIDTH = 9;
        public const int TUFT_HEIGHT = 12;
        private const int TUFT_VARIANTS = 8;
        private static Texture2D _tuftTexture;

        public static bool CanHaveTufts(ushort graphic)
        {
            EnsureLoaded();

            return graphic < _tuftLand.Length && _tuftLand[graphic];
        }

        public static int GetTuftCount(int x, int y)
        {
            uint h = Hash(x, y, 0x5A17) % 100;

            return h < 25 ? 0 : h < 60 ? 1 : h < 90 ? 2 : 3;
        }

        // Bottom-center offset of tuft i from the tile's diamond center, and its texture variant.
        // The 9x12 sprite must stay inside the 44x44 diamond (|x| + |y| <= 22 from its center):
        // bottom corners need |dx| + 4.5 + dy <= 22, top corners |dx| + 4.5 + (12 - dy) <= 22.
        public static void GetTuft(int x, int y, int i, out int dx, out int dy, out int variant)
        {
            uint h = Hash(x, y, 0x3C1 + i * 7919);
            dx = (int)(h % 21) - 10;
            int ax = Math.Abs(dx);
            int minDy = Math.Max(2, ax - 5);
            int maxDy = 17 - ax;
            dy = minDy + (int)((h >> 8) % (uint)(maxDy - minDy + 1));
            variant = (int)((h >> 16) % TUFT_VARIANTS);
        }

        public static Rectangle GetTuftSource(int variant)
        {
            return new Rectangle(variant * TUFT_WIDTH, 0, TUFT_WIDTH, TUFT_HEIGHT);
        }

        public static Texture2D GetTuftTexture(GraphicsDevice device)
        {
            if (_tuftTexture != null && !_tuftTexture.IsDisposed)
            {
                return _tuftTexture;
            }

            int width = TUFT_WIDTH * TUFT_VARIANTS;
            var pixels = new Color[width * TUFT_HEIGHT];
            var dark = new Vector3(38, 62, 20);
            var light = new Vector3(122, 160, 60);

            for (int v = 0; v < TUFT_VARIANTS; v++)
            {
                var rnd = new Random(0x7F00 + v);
                int blades = 3 + rnd.Next(3);

                for (int b = 0; b < blades; b++)
                {
                    int baseX = 1 + rnd.Next(TUFT_WIDTH - 2);
                    int height = 5 + rnd.Next(TUFT_HEIGHT - 4);
                    int lean = rnd.Next(3) - 1;

                    for (int t = 0; t < height; t++)
                    {
                        int px = baseX + (int)Math.Round(lean * 2f * t / height);
                        int py = TUFT_HEIGHT - 1 - t;

                        if (px < 0 || px >= TUFT_WIDTH)
                        {
                            continue;
                        }

                        Vector3 c = Vector3.Lerp(dark, light, t / (float)height);
                        pixels[py * width + v * TUFT_WIDTH + px] = new Color((byte)c.X, (byte)c.Y, (byte)c.Z, (byte)255);

                        // a slightly thicker base reads better at 1x zoom
                        if (t < 2 && px + 1 < TUFT_WIDTH)
                        {
                            pixels[py * width + v * TUFT_WIDTH + px + 1] = new Color((byte)dark.X, (byte)dark.Y, (byte)dark.Z, (byte)255);
                        }
                    }
                }
            }

            _tuftTexture = new Texture2D(device, width, TUFT_HEIGHT, false, SurfaceFormat.Color);
            _tuftTexture.SetData(pixels);

            return _tuftTexture;
        }

        private static uint Hash(int x, int y, int salt)
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)salt * 0xCB1AB31Fu;
            h ^= h >> 13;
            h *= 0x85EBCA6Bu;
            h ^= h >> 16;

            return h;
        }

        private static bool Contains(string s, string value) => s.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
