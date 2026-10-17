// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;
using ClassicUO.Renderer.Lighting;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Game.Managers
{
    // Profile.HdWorld: land tiles, texmaps and statics drawn from 2x images of an HD pack in
    // Data/HD/world (made with tools/hdpack, never shipped: it is derived from EA's art and every
    // player builds it locally). At every zoom level: zoomed in (art pixels larger than screen
    // pixels) the 2x images are used as they are, otherwise they are halved while loading, so the
    // remastered look also shows at 1x, drawn pixel for pixel like the original art.
    //
    // Images are decoded on the thread pool when first needed and uploaded to a separate atlas on
    // the render thread. Geometry, picking and draw order keep using the original graphics; only
    // the texture the pixels are sampled from changes.
    //
    // With Profile.SurfaceLighting a normal map is made from every image while decoding and
    // uploaded to a page beside its atlas page, at the same place (docs/SURFACE_LIGHTING_PLAN.md).
    internal static class HdWorldPack
    {
        public enum Kind
        {
            Land,
            Texmap,
            Static
        }

        private const float MIN_RASTER_SCALE = 1.25f;
        private const int ATLAS_SIZE = 4096;
        // ~4 pages of 4096x4096 RGBA (256 MB of VRAM); past that the pack starts over
        private const long PIXEL_BUDGET = (long)ATLAS_SIZE * ATLAS_SIZE * 4 * 85 / 100;
        private const int MAX_PENDING = 128;
        private const int MAX_UPLOADS_PER_FRAME = 32;
        // new HD textures are folded into the chunk meshes in batches, not on every upload
        private const uint MESH_REFRESH_INTERVAL_MS = 400;

        private static readonly Dictionary<int, string>[] _index = new Dictionary<int, string>[3];
        private static readonly Dictionary<string, Entry> _byFile = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentQueue<Loaded> _done = new();
        // normal map page beside each atlas page
        private static readonly Dictionary<Texture2D, Texture2D> _normalPages = new();
        private static bool _normals;
        private static GraphicsDevice _device;
        private static TextureAtlas _atlas;
        private static bool _indexLoaded;
        private static int _pending;
        // bumped when the atlas starts over: loads requested before that are dropped
        private static int _epoch;
        private static int _generation;
        private static int _textureScale = 1;
        private static long _usedPixels;
        private static bool _uploadedSinceRefresh;
        private static uint _lastRefresh;

        // size of the loaded textures relative to the original art: 2 when zoomed in, else 1
        public static int TextureScale => _textureScale;

        public static string PackDirectory => Path.Combine(CUOEnviroment.ExecutablePath, "Data", "HD", "world");

        public static bool Active =>
            ProfileManager.CurrentProfile != null
            && ProfileManager.CurrentProfile.HdWorld
            && _atlas != null
            && HasContent;

        // Chunk meshes remember the value they were built with and rebuild when it changes:
        // -1 while inactive, otherwise bumped as batches of HD textures become available.
        public static int MeshGeneration => Active ? _generation : -1;

        // the option is on and there is a pack to draw from (the zoom level aside)
        public static bool HasPack =>
            ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.HdWorld && HasContent;

        private static bool HasContent => _index[0]?.Count > 0 || _index[1]?.Count > 0 || _index[2]?.Count > 0;

        public static void Update(GraphicsDevice device)
        {
            AutoEnable();

            if (ProfileManager.CurrentProfile == null || !ProfileManager.CurrentProfile.HdWorld)
            {
                if (_atlas != null)
                {
                    // option turned off: give the VRAM back
                    Clear(false);
                }

                return;
            }

            _device = device;

            if (!_indexLoaded)
            {
                _indexLoaded = true;
                LoadIndex();
            }

            _atlas ??= new TextureAtlas(device, ATLAS_SIZE, ATLAS_SIZE, SurfaceFormat.Color);

            int textureScale = HdCharacterSprites.RasterScale >= MIN_RASTER_SCALE ? 2 : 1;
            bool normals = ProfileManager.CurrentProfile.SurfaceLighting;

            if (textureScale != _textureScale || normals != _normals)
            {
                // zoomed in or out past the threshold, or the surface lighting switched: load
                // everything again
                _textureScale = textureScale;
                _normals = normals;
                Clear(true);

                return;
            }

            for (int i = 0; i < MAX_UPLOADS_PER_FRAME && _done.TryDequeue(out Loaded loaded); i++)
            {
                if (loaded.Epoch != _epoch || !_byFile.TryGetValue(loaded.File, out Entry entry))
                {
                    continue;
                }

                _pending--;

                if (loaded.Pixels == null)
                {
                    entry.Failed = true;

                    continue;
                }

                long pixels = (long)loaded.Width * loaded.Height;

                if (_usedPixels + pixels > PIXEL_BUDGET)
                {
                    // start over; meshes rebuild with originals and request what is visible again
                    Clear(true);

                    return;
                }

                _usedPixels += pixels;
                entry.Texture = _atlas.AddSprite(loaded.Pixels, loaded.Width, loaded.Height, out entry.Uv);
                _uploadedSinceRefresh = true;

                if (loaded.Normals != null)
                {
                    GetNormalPage(entry.Texture).SetData(0, entry.Uv, loaded.Normals, 0, loaded.Normals.Length);
                }
            }

            if (_uploadedSinceRefresh && Time.Ticks - _lastRefresh >= MESH_REFRESH_INTERVAL_MS)
            {
                _uploadedSinceRefresh = false;
                _lastRefresh = Time.Ticks;
                _generation++;
            }
        }

        // A pack installed by the launcher is switched on once per profile, without the player
        // looking for the option; after that the option is left to the player.
        private static Profile _autoEnableChecked;

        private static void AutoEnable()
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (profile == null || ReferenceEquals(profile, _autoEnableChecked))
            {
                return;
            }

            _autoEnableChecked = profile;

            if (profile.HdWorldAutoEnabled || !File.Exists(Path.Combine(PackDirectory, "index.json")))
            {
                return;
            }

            profile.HdWorldAutoEnabled = true;
            profile.HdWorld = true;
            Log.Info("HD world: pack found, option switched on");
        }

        // The HD texture for a graphic, or false (and starts loading it if the pack has one).
        public static bool TryGet(Kind kind, int id, out Texture2D texture, out Rectangle uv)
        {
            texture = null;
            uv = Rectangle.Empty;

            if (!Active)
            {
                return false;
            }

            Dictionary<int, string> index = _index[(int)kind];

            if (index == null || !index.TryGetValue(id, out string file))
            {
                return false;
            }

            if (_byFile.TryGetValue(file, out Entry entry))
            {
                if (entry.Texture != null && !entry.Texture.IsDisposed)
                {
                    texture = entry.Texture;
                    uv = entry.Uv;

                    return true;
                }

                return false;
            }

            if (_pending >= MAX_PENDING)
            {
                return false;
            }

            _byFile[file] = new Entry();
            _pending++;
            int epoch = _epoch;
            bool half = _textureScale == 1;
            bool normals = _normals;
            string path = Path.Combine(PackDirectory, file);

            Task.Run(() => _done.Enqueue(Decode(file, path, epoch, half, normals)));

            return false;
        }

        private static Texture2D GetNormalPage(Texture2D page)
        {
            if (!_normalPages.TryGetValue(page, out Texture2D normals))
            {
                normals = new Texture2D(_device, page.Width, page.Height, false, SurfaceFormat.Color);
                _normalPages[page] = normals;
                SurfaceNormals.Register(page, normals);
            }

            return normals;
        }

        private static Loaded Decode(string file, string path, int epoch, bool half, bool normals)
        {
            try
            {
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
                var rgba = new Rgba32[image.Width * image.Height];
                image.CopyPixelDataTo(rgba);

                // FNA SurfaceFormat.Color is RGBA bytes; the world blend state expects premultiplied alpha
                uint[] pixels = MemoryMarshal.Cast<Rgba32, uint>(rgba).ToArray();

                for (int i = 0; i < pixels.Length; i++)
                {
                    uint a = pixels[i] >> 24;

                    if (a == 0)
                    {
                        pixels[i] = 0;
                    }
                    else if (a != 255)
                    {
                        uint r = (pixels[i] & 0xFF) * a / 255;
                        uint g = ((pixels[i] >> 8) & 0xFF) * a / 255;
                        uint b = ((pixels[i] >> 16) & 0xFF) * a / 255;
                        pixels[i] = (a << 24) | (b << 16) | (g << 8) | r;
                    }
                }

                int width = image.Width;
                int height = image.Height;

                if (half)
                {
                    pixels = Halve(pixels, width, height);
                    width /= 2;
                    height /= 2;
                }

                uint[] normalMap = normals ? SurfaceNormalBuilder.Build(pixels, width, height) : null;

                return new Loaded(file, epoch, pixels, normalMap, width, height);
            }
            catch (Exception ex)
            {
                Log.Warn($"HD world: cannot load {path}: {ex.Message}");

                return new Loaded(file, epoch, null, null, 0, 0);
            }
        }

        // 2x2 box average of premultiplied RGBA. The pack keeps the original outline at 2x, so every
        // 2x2 block is fully opaque or fully transparent and edges stay as sharp as the original.
        internal static uint[] Halve(uint[] src, int width, int height)
        {
            int w = width / 2;
            int h = height / 2;
            var dst = new uint[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * 2 * width + x * 2;
                    uint a = src[i], b = src[i + 1], c = src[i + width], d = src[i + width + 1];
                    uint result = 0;

                    for (int shift = 0; shift < 32; shift += 8)
                    {
                        uint sum = ((a >> shift) & 0xFF) + ((b >> shift) & 0xFF) + ((c >> shift) & 0xFF) + ((d >> shift) & 0xFF);
                        result |= ((sum + 2) / 4) << shift;
                    }

                    dst[y * w + x] = result;
                }
            }

            return dst;
        }

        private static void LoadIndex()
        {
            string path = Path.Combine(PackDirectory, "index.json");

            if (!File.Exists(path))
            {
                Log.Info($"HD world: no pack at {PackDirectory}");

                return;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                string[] names = { "land", "texmaps", "statics" };

                for (int k = 0; k < names.Length; k++)
                {
                    var map = new Dictionary<int, string>();

                    if (doc.RootElement.TryGetProperty(names[k], out JsonElement section))
                    {
                        foreach (JsonProperty p in section.EnumerateObject())
                        {
                            string rel = p.Value.GetString();

                            // ids are decimal; files must stay inside the pack folder
                            if (int.TryParse(p.Name, out int id) && rel != null && !rel.Contains("..") && !Path.IsPathRooted(rel))
                            {
                                map[id] = rel;
                            }
                        }
                    }

                    _index[k] = map;
                }

                Log.Info($"HD world: {_index[0].Count} land, {_index[1].Count} texmaps, {_index[2].Count} statics");
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Log.Error($"HD world: bad index {path}: {ex.Message}");
            }
        }

        private static void Clear(bool newAtlas)
        {
            foreach (KeyValuePair<Texture2D, Texture2D> pair in _normalPages)
            {
                SurfaceNormals.Unregister(pair.Key);
                pair.Value.Dispose();
            }

            _normalPages.Clear();
            _atlas?.Dispose();
            _atlas = newAtlas && _device != null ? new TextureAtlas(_device, ATLAS_SIZE, ATLAS_SIZE, SurfaceFormat.Color) : null;
            _byFile.Clear();
            _usedPixels = 0;
            _pending = 0;
            _epoch++;
            // every mesh built with the old textures rebuilds before it is drawn again
            _generation++;
        }

        private sealed class Entry
        {
            public Texture2D Texture;
            public Rectangle Uv;
            public bool Failed;
        }

        private readonly record struct Loaded(string File, int Epoch, uint[] Pixels, uint[] Normals, int Width, int Height);
    }
}
