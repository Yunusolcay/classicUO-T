// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MathHelper = Microsoft.Xna.Framework.MathHelper;

namespace ClassicUO.Game
{
    // Region atmosphere (docs/ATMOSPHERE_PLAN.md): the ground around the player tells the kind of place
    // (dungeon, snow, desert, jungle, acid swamp, volcanic, void) and the world gets that place's color
    // tint and a light haze, fading over a few seconds when walking from one to another.
    internal sealed class AtmosphereRenderer
    {
        internal enum Region
        {
            Neutral,
            Dungeon,
            Snow,
            Desert,
            Jungle,
            Swamp,
            Volcanic,
            Void,
            Count
        }

        private struct Look
        {
            public Vector3 Tint;
            public Vector3 Fog;
            public float FogAlpha;

            public Look(Vector3 tint, Vector3 fog, float fogAlpha)
            {
                Tint = tint;
                Fog = fog;
                FogAlpha = fogAlpha;
            }
        }

        private static readonly Look[] _looks =
        {
            new Look(new Vector3(1f, 1f, 1f), Vector3.Zero, 0f),
            new Look(new Vector3(0.84f, 0.9f, 0.96f), new Vector3(0.10f, 0.14f, 0.17f), 0.12f),
            new Look(new Vector3(0.94f, 0.97f, 1f), new Vector3(0.86f, 0.9f, 1f), 0.08f),
            new Look(new Vector3(1f, 0.95f, 0.86f), new Vector3(0.92f, 0.8f, 0.58f), 0.06f),
            new Look(new Vector3(0.9f, 1f, 0.88f), new Vector3(0.45f, 0.6f, 0.4f), 0.07f),
            new Look(new Vector3(0.86f, 0.96f, 0.8f), new Vector3(0.35f, 0.45f, 0.25f), 0.10f),
            new Look(new Vector3(1f, 0.86f, 0.78f), new Vector3(0.55f, 0.16f, 0.05f), 0.08f),
            new Look(new Vector3(0.88f, 0.84f, 1f), new Vector3(0.2f, 0.1f, 0.3f), 0.12f)
        };

        private const int SAMPLE_RADIUS = 6;
        private const uint SAMPLE_INTERVAL_MS = 500;
        // seconds for the look to follow the place
        private const float FOLLOW_SECONDS = 3f;
        // first dungeon column of the Felucca/Trammel maps
        private const int DUNGEON_X = 5120;

        private static readonly BlendState _multiply = new BlendState
        {
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.SourceColor,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One
        };

        private readonly Dictionary<ushort, Region> _landRegions = new Dictionary<ushort, Region>();
        private readonly float[] _weights = new float[(int)Region.Count];
        private Look _target = _looks[0];
        private Look _current = _looks[0];
        private uint _nextSample;
        // 1x1 textures holding this frame's tint and fog color: the default shader only colors by hue
        // index, and a texture cache per color would grow without end while the colors fade
        private Texture2D _tintTexture, _fogTexture;

        public void Reset()
        {
            _current = _target = _looks[0];
            _nextSample = 0;
        }

        public void Draw(UltimaBatcher2D batcher, World world, Vector2 viewOrigin, float scale, int width, int height, float layerDepth, float delta)
        {
            if (world.Player == null || world.Map == null)
            {
                return;
            }

            if (Time.Ticks >= _nextSample)
            {
                _nextSample = Time.Ticks + SAMPLE_INTERVAL_MS;
                _target = Sample(world);
            }

            float k = 1f - (float)Math.Exp(-Math.Clamp(delta, 0f, 0.1f) * 3f / FOLLOW_SECONDS);
            _current.Tint = Vector3.Lerp(_current.Tint, _target.Tint, k);
            _current.Fog = Vector3.Lerp(_current.Fog, _target.Fog, k);
            _current.FogAlpha = MathHelper.Lerp(_current.FogAlpha, _target.FogAlpha, k);

            Rectangle view = new Rectangle((int)viewOrigin.X, (int)viewOrigin.Y, (int)(width * scale) + 1, (int)(height * scale) + 1);

            if (Vector3.DistanceSquared(_current.Tint, Vector3.One) > 0.00002f)
            {
                SetColor(batcher.GraphicsDevice, ref _tintTexture, _current.Tint);
                batcher.SetBlendState(_multiply);
                batcher.Draw(_tintTexture, view, Vector3.UnitZ, layerDepth);
                batcher.SetBlendState(null);
            }

            if (_current.FogAlpha > 0.002f)
            {
                SetColor(batcher.GraphicsDevice, ref _fogTexture, _current.Fog);
                batcher.Draw(_fogTexture, view, new Vector3(0, 0, _current.FogAlpha), layerDepth);
            }
        }

        // called once per frame per texture, before the batch that uses it is flushed
        private static void SetColor(GraphicsDevice device, ref Texture2D texture, Vector3 color)
        {
            if (texture == null || texture.IsDisposed)
            {
                texture = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
            }

            texture.SetData(new[] { new Color(color) });
        }

        // Share of each kind of place in the ground around the player; the look is their mix
        private Look Sample(World world)
        {
            Array.Clear(_weights);

            int px = world.Player.X;
            int py = world.Player.Y;
            int samples = 0;

            if ((world.MapIndex == 0 || world.MapIndex == 1) && px >= DUNGEON_X)
            {
                return _looks[(int)Region.Dungeon];
            }

            for (int y = py - SAMPLE_RADIUS; y <= py + SAMPLE_RADIUS; y += 2)
            {
                for (int x = px - SAMPLE_RADIUS; x <= px + SAMPLE_RADIUS; x += 2)
                {
                    if (x < 0 || y < 0)
                    {
                        continue;
                    }

                    for (GameObject obj = world.Map.GetTile(x, y, false); obj != null; obj = obj.TNext)
                    {
                        if (obj is Land land)
                        {
                            _weights[(int)GetRegion(land.Graphic)]++;
                            samples++;

                            break;
                        }
                    }
                }
            }

            if (samples == 0)
            {
                return _looks[0];
            }

            Look look = new Look(Vector3.Zero, Vector3.Zero, 0f);

            for (int i = 0; i < _weights.Length; i++)
            {
                float w = _weights[i] / samples;

                if (w <= 0f)
                {
                    continue;
                }

                look.Tint += _looks[i].Tint * w;
                look.Fog += _looks[i].Fog * w * _looks[i].FogAlpha;
                look.FogAlpha += _looks[i].FogAlpha * w;
            }

            // fog color weighted by how much each place fogs
            if (look.FogAlpha > 0f)
            {
                look.Fog /= look.FogAlpha;
            }

            return look;
        }

        private Region GetRegion(ushort graphic)
        {
            if (_landRegions.TryGetValue(graphic, out Region region))
            {
                return region;
            }

            string name = Client.Game.UO.FileManager.TileData.LandData[graphic].Name?.ToLowerInvariant() ?? string.Empty;
            region = Classify(name);
            _landRegions[graphic] = region;

            return region;
        }

        // by the tiledata land name ("decimated_" variants included)
        internal static Region Classify(string name)
        {
            if (name.Contains("snow")) return Region.Snow;
            if (name.Contains("cave")) return Region.Dungeon;
            if (name.Contains("lava") || name.Contains("obsidian")) return Region.Volcanic;
            if (name.Contains("acid")) return Region.Swamp;
            if (name.Contains("jungle")) return Region.Jungle;
            if (name.Contains("void")) return Region.Void;
            if (name.Contains("sand") && !name.Contains("sand stone")) return Region.Desert;

            return Region.Neutral;
        }
    }
}
