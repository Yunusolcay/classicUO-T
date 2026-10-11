// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Renderer.Animations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    // Profile.HdCharacters: draw mobiles and their equipment from 2x xBR frames while zoomed in.
    // Purely visual: sizes, positions, draw order slices and mouse picking keep using the original
    // frame, only the texture the pixels come from changes.
    internal static class HdCharacterSprites
    {
        // below this magnification a 2x frame cannot show more detail than the original
        private const float MIN_RASTER_SCALE = 1.25f;

        private static HdSpriteCache _cache;

        // Screen pixels per art pixel the world is currently rasterized at; set by GameScene each
        // frame (1 while the xBR world filter draws at 1:1 art scale).
        public static float RasterScale { get; set; } = 1f;

        public static bool Active =>
            RasterScale >= MIN_RASTER_SCALE && ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.HdCharacters;

        public static void Update(GraphicsDevice device)
        {
            if (ProfileManager.CurrentProfile == null || !ProfileManager.CurrentProfile.HdCharacters)
            {
                // option turned off: give the VRAM back (just zooming out keeps it for zooming in again)
                _cache?.Dispose();
                _cache = null;

                return;
            }

            if (!Active)
            {
                return;
            }

            _cache ??= new HdSpriteCache(device);
            _cache.Update();
        }

        // Maps a source rectangle inside the original frame (whole frame or a horizontal slice of
        // it) to the HD texture. Returns false when the HD frame is not ready (yet).
        public static bool TryMap(Texture2D texture, Rectangle frameUv, Rectangle sourceRect, out Texture2D hdTexture, out Rectangle hdSourceRect)
        {
            hdSourceRect = Rectangle.Empty;
            hdTexture = null;

            if (_cache == null || !Active || !_cache.TryGet(texture, frameUv, out hdTexture, out Rectangle hdUv))
            {
                return false;
            }

            hdSourceRect = MapToHd(frameUv, hdUv, sourceRect);

            return true;
        }

        internal static Rectangle MapToHd(Rectangle frameUv, Rectangle hdUv, Rectangle sourceRect)
        {
            const int s = HdSpriteCache.SCALE;

            return new Rectangle(
                hdUv.X + (sourceRect.X - frameUv.X) * s,
                hdUv.Y + (sourceRect.Y - frameUv.Y) * s,
                sourceRect.Width * s,
                sourceRect.Height * s
            );
        }

        public static void Reset() => _cache?.Reset();
    }
}
