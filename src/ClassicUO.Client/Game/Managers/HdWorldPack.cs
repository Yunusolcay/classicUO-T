// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Game.Managers
{
    // Profile.HdWorld: land tiles, texmaps and statics drawn from 2x images of an HD pack in
    // Data/HD/world (made with tools/hdpack, never shipped: it is derived from EA's art and every
    // player builds it locally). Only while zoomed in, like HdCharacterSprites.
    //
    // Images are decoded on the thread pool when first needed and uploaded to a separate atlas on
    // the render thread. Geometry, picking and draw order keep using the original graphics; only
    // the texture the pixels are sampled from changes.
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
        private static GraphicsDevice _device;
        private static TextureAtlas _atlas;
        private static bool _indexLoaded;
        private static int _pending;
        private static int _generation;
        private static long _usedPixels;
        private static bool _uploadedSinceRefresh;
        private static uint _lastRefresh;

        public static string PackDirectory => Path.Combine(CUOEnviroment.ExecutablePath, "Data", "HD", "world");

        public static bool Active =>
            HdCharacterSprites.RasterScale >= MIN_RASTER_SCALE
            && ProfileManager.CurrentProfile != null
            && ProfileManager.CurrentProfile.HdWorld
            && _atlas != null
            && HasContent;

        // Chunk meshes remember the value they were built with and rebuild when it changes:
        // -1 while inactive, otherwise bumped as batches of HD textures become available.
        public static int MeshGeneration => Active ? _generation : -1;

        private static bool HasContent => _index[0]?.Count > 0 || _index[1]?.Count > 0 || _index[2]?.Count > 0;

        public static void Update(GraphicsDevice device)
        {
            if (ProfileManager.CurrentProfile == null || !ProfileManager.CurrentProfile.HdWorld)
            {
                if (_atlas != null)
                {
                    // option turned off: give the VRAM back
                    Clear();
                    _atlas.Dispose();
                    _atlas = null;
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

            for (int i = 0; i < MAX_UPLOADS_PER_FRAME && _done.TryDequeue(out Loaded loaded); i++)
            {
                if (loaded.Generation != _generation || !_byFile.TryGetValue(loaded.File, out Entry entry))
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
                    Clear();

                    return;
                }

                _usedPixels += pixels;
                entry.Texture = _atlas.AddSprite(loaded.Pixels, loaded.Width, loaded.Height, out entry.Uv);
                _uploadedSinceRefresh = true;
            }

            if (_uploadedSinceRefresh && Time.Ticks - _lastRefresh >= MESH_REFRESH_INTERVAL_MS)
            {
                _uploadedSinceRefresh = false;
                _lastRefresh = Time.Ticks;
                _generation++;
            }
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
            int generation = _generation;
            string path = Path.Combine(PackDirectory, file);

            Task.Run(() => _done.Enqueue(Decode(file, path, generation)));

            return false;
        }

        private static Loaded Decode(string file, string path, int generation)
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

                return new Loaded(file, generation, pixels, image.Width, image.Height);
            }
            catch (Exception ex)
            {
                Log.Warn($"HD world: cannot load {path}: {ex.Message}");

                return new Loaded(file, generation, null, 0, 0);
            }
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

        private static void Clear()
        {
            _atlas?.Dispose();
            _atlas = _device != null ? new TextureAtlas(_device, ATLAS_SIZE, ATLAS_SIZE, SurfaceFormat.Color) : null;
            _byFile.Clear();
            _usedPixels = 0;
            _pending = 0;
            // every mesh built with the old textures rebuilds before it is drawn again
            _generation++;
        }

        private sealed class Entry
        {
            public Texture2D Texture;
            public Rectangle Uv;
            public bool Failed;
        }

        private readonly record struct Loaded(string File, int Generation, uint[] Pixels, int Width, int Height);
    }
}
