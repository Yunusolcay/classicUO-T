// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Animations
{
    // 2x xBR versions of animation frames (XbrSprite), made on demand for zoomed-in drawing.
    //
    // Source pixels are read back from the frame already sitting in the normal animation atlas, so
    // the original frame (position, center, mouse picking mask) stays the single source of truth
    // and an HD frame can always be thrown away and rebuilt. Magnification runs on the thread pool;
    // only the GPU readback and upload happen on the render thread, both rate limited per frame.
    public sealed class HdSpriteCache : IDisposable
    {
        public const int SCALE = 2;

        private const int ATLAS_SIZE = 4096;
        // ~2 pages of 4096x4096 RGBA (128 MB of VRAM) allowing for packing waste; past that the
        // cache starts over and frames are rebuilt as they are drawn again
        private const long PIXEL_BUDGET = (long)ATLAS_SIZE * ATLAS_SIZE * 2 * 85 / 100;
        private const int MAX_READBACKS_PER_FRAME = 6;
        private const int MAX_UPLOADS_PER_FRAME = 24;
        private const int MAX_PENDING = 96;
        // frames bigger than this are not worth the memory (and would not fit an atlas page)
        private const int MAX_SOURCE_SIZE = 512;

        private readonly GraphicsDevice _device;
        private readonly Dictionary<Key, Entry> _entries = new();
        private readonly ConcurrentQueue<Result> _done = new();
        private TextureAtlas _atlas;
        private int _generation;
        private int _pending;
        private int _readbacksThisFrame;
        private long _usedPixels;
        private uint[] _readbackBuffer = new uint[64 * 64];

        public HdSpriteCache(GraphicsDevice device)
        {
            _device = device;
            _atlas = new TextureAtlas(device, ATLAS_SIZE, ATLAS_SIZE, SurfaceFormat.Color);
        }

        public int ReadyCount { get; private set; }

        // HD texture and source rectangle for an atlas frame, or false (and schedules the work)
        public bool TryGet(Texture2D texture, Rectangle uv, out Texture2D hdTexture, out Rectangle hdUv)
        {
            hdTexture = null;
            hdUv = Rectangle.Empty;

            if (texture == null || texture.IsDisposed || uv.Width <= 0 || uv.Height <= 0
                || uv.Width > MAX_SOURCE_SIZE || uv.Height > MAX_SOURCE_SIZE)
            {
                return false;
            }

            var key = new Key(texture, uv);

            if (_entries.TryGetValue(key, out Entry entry))
            {
                if (entry.Texture != null && !entry.Texture.IsDisposed)
                {
                    hdTexture = entry.Texture;
                    hdUv = entry.Uv;

                    return true;
                }

                return false; // still being made
            }

            if (_readbacksThisFrame >= MAX_READBACKS_PER_FRAME || _pending >= MAX_PENDING)
            {
                return false;
            }

            _readbacksThisFrame++;

            int count = uv.Width * uv.Height;

            if (_readbackBuffer.Length < count)
            {
                _readbackBuffer = new uint[count];
            }

            try
            {
                texture.GetData(0, uv, _readbackBuffer, 0, count);
            }
            catch (Exception ex)
            {
                Log.Warn($"HD sprite readback failed: {ex.Message}");
                _entries[key] = new Entry(); // do not retry this frame source every frame

                return false;
            }

            // the buffer is reused next readback: the worker gets its own copy
            uint[] source = _readbackBuffer.AsSpan(0, count).ToArray();
            int generation = _generation;
            int width = uv.Width, height = uv.Height;

            _entries[key] = new Entry();
            _pending++;

            Task.Run(() =>
            {
                uint[] pixels = null;

                try
                {
                    pixels = XbrSprite.Magnify(source, width, height);
                }
                catch (Exception ex)
                {
                    Log.Warn($"HD sprite magnification failed: {ex.Message}");
                }

                _done.Enqueue(new Result(key, generation, pixels, width * SCALE, height * SCALE));
            });

            return false;
        }

        // Once per frame on the render thread, before drawing the world.
        public void Update()
        {
            _readbacksThisFrame = 0;

            for (int i = 0; i < MAX_UPLOADS_PER_FRAME && _done.TryDequeue(out Result result); i++)
            {
                if (result.Generation != _generation)
                {
                    continue; // cache was reset while this was in flight
                }

                _pending--;

                if (result.Pixels == null || !_entries.TryGetValue(result.Key, out Entry entry))
                {
                    continue;
                }

                long pixels = (long)result.Width * result.Height;

                if (_usedPixels + pixels > PIXEL_BUDGET)
                {
                    Reset();

                    return;
                }

                _usedPixels += pixels;
                entry.Texture = _atlas.AddSprite(result.Pixels, result.Width, result.Height, out entry.Uv);
                ReadyCount++;
            }
        }

        public void Reset()
        {
            _atlas.Dispose();
            _atlas = new TextureAtlas(_device, ATLAS_SIZE, ATLAS_SIZE, SurfaceFormat.Color);
            _entries.Clear();
            _generation++;
            _pending = 0;
            _usedPixels = 0;
            ReadyCount = 0;
        }

        public void Dispose()
        {
            _atlas?.Dispose();
            _entries.Clear();
        }

        private readonly record struct Key(Texture2D Texture, Rectangle Uv);

        private sealed class Entry
        {
            public Texture2D Texture;
            public Rectangle Uv;
        }

        private readonly record struct Result(Key Key, int Generation, uint[] Pixels, int Width, int Height);
    }
}
