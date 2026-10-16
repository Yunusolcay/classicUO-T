// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Lighting
{
    // Collects the light sources of a frame and draws them into the light map.
    // Light resolution and drawing match the original GameScene light code.
    internal sealed class LightManager
    {
        public const int MAX_LIGHTS_PER_FRAME = 512;
        public const int LEGACY_MAX_LIGHTS = 100;

        private readonly World _world;
        private readonly LightSource[] _lights = new LightSource[MAX_LIGHTS_PER_FRAME];
        private int _count;
        private int _maxLights = LEGACY_MAX_LIGHTS;

        public LightManager(World world)
        {
            _world = world;
        }

        public int Count => _count;

        public int MaxLights
        {
            get => _maxLights;
            set => _maxLights = Math.Clamp(value, 0, MAX_LIGHTS_PER_FRAME);
        }

        // maxZ: the scene's current draw z limit; lights under a roof above it are skipped
        public void Add(GameObject obj, GameObject lightObject, int x, int y, int maxZ)
        {
            if (_count >= MaxLights || obj == null)
            {
                return;
            }

            if (IsOccluded(obj, maxZ))
            {
                return;
            }

            LightSource light = default;

            ushort graphic = lightObject.Graphic;

            if (
                graphic >= 0x3E02 && graphic <= 0x3E0B
                || graphic >= 0x3914 && graphic <= 0x3929
                || graphic == 0x0B1D
            )
            {
                light.ID = 2;
            }
            else
            {
                if (obj == lightObject && obj is Item item)
                {
                    light.ID = item.LightID;
                }
                else if (lightObject is Item it)
                {
                    light.ID = (byte)it.ItemData.LightIndex;

                    if (obj is Mobile mob)
                    {
                        switch (mob.Direction)
                        {
                            case Direction.Right:
                                y += 33;
                                x += 22;

                                break;

                            case Direction.Left:
                                y += 33;
                                x -= 22;

                                break;

                            case Direction.East:
                                x += 22;
                                y += 55;

                                break;

                            case Direction.Down:
                                y += 55;

                                break;

                            case Direction.South:
                                x -= 22;
                                y += 55;

                                break;
                        }
                    }
                }
                else if (obj is Mobile _)
                {
                    light.ID = 1;
                }
                else
                {
                    ref StaticTiles data = ref Client.Game.UO.FileManager.TileData.StaticData[obj.Graphic];
                    light.ID = data.Layer;
                }
            }

            light.Color = 0;
            light.IsHue = false;

            if (ProfileManager.CurrentProfile.UseColoredLights)
            {
                if (light.ID > 200)
                {
                    light.Color = (ushort)(light.ID - 200);
                    light.ID = 1;
                }

                if (LightColors.GetHue(graphic, out ushort color, out bool ishue))
                {
                    light.Color = color;
                    light.IsHue = ishue;
                }
            }

            if (light.Color != 0)
            {
                light.Color++;
            }

            light.DrawX = x;
            light.DrawY = y;
            light.HasDefinition = LightDefinitions.TryGet(lightObject.Graphic, out light.Definition);
            light.Seed = lightObject is Entity entity ? (int)entity.Serial : lightObject.X * 7919 + lightObject.Y * 104729;
            light.Carried = obj is Mobile;
            TryAdd(light);
        }

        // Appends a resolved light without coupling collection limits to asset ID limits.
        public bool TryAdd(in LightSource light)
        {
            if (_count >= MaxLights || light.ID >= LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT)
            {
                return false;
            }

            _lights[_count++] = light;

            return true;
        }

        // Lights of this frame that cast soft shadows (docs/SHADOWS_PLAN.md, S3), in world render
        // target pixels. Call before Draw, which starts a new frame of collection.
        public int GetShadowLights(Span<ShadowLight> destination)
        {
            int written = 0;

            for (int i = 0; i < _count && written < destination.Length; i++)
            {
                ref LightSource l = ref _lights[i];

                if (l.Carried)
                {
                    continue;
                }

                ref readonly var lightInfo = ref Client.Game.UO.Lights.GetLight(l.ID);

                if (lightInfo.Texture == null)
                {
                    continue;
                }

                float size = Math.Max(lightInfo.UV.Width, lightInfo.UV.Height);
                bool defined = l.HasDefinition;

                destination[written++] = new ShadowLight
                {
                    Position = new Vector2(l.DrawX, l.DrawY),
                    // the light reaches about half its sprite size; shadows go a bit further out
                    Radius = size * 0.6f * (defined ? l.Definition.Radius : 1f),
                    Strength = defined ? l.Definition.Intensity : 0.8f
                };
            }

            return written;
        }

        // Draws the collected lights with the batcher already begun on the light render target,
        // then starts a new frame of collection.
        public void Draw(UltimaBatcher2D batcher)
        {
            Draw(batcher, default);
        }

        // options.UseDefinitions (enhanced lighting): lights_ex.txt size, strength, flicker and
        // round shape. Without it every light draws exactly like the original code.
        public void Draw(UltimaBatcher2D batcher, in LightDrawOptions options)
        {
            Vector3 hue = Vector3.Zero;

            for (int i = 0; i < _count; i++)
            {
                ref LightSource l = ref _lights[i];
                ref readonly var lightInfo = ref Client.Game.UO.Lights.GetLight(l.ID);

                if (lightInfo.Texture == null)
                {
                    continue;
                }

                hue.Z = 1f;

                hue.X = l.Color;
                hue.Y =
                    hue.X > 1.0f
                        ? l.IsHue
                            ? ShaderHueTranslator.SHADER_HUED
                            : ShaderHueTranslator.SHADER_LIGHTS
                        : ShaderHueTranslator.SHADER_NONE;

                if (options.UseDefinitions && l.HasDefinition)
                {
                    DrawDefined(batcher, ref l, lightInfo.Texture, lightInfo.UV, hue, options);

                    continue;
                }

                batcher.Draw(
                    lightInfo.Texture,
                    new Vector2(
                        l.DrawX - lightInfo.UV.Width * 0.5f,
                        l.DrawY - lightInfo.UV.Height * 0.5f
                    ),
                    lightInfo.UV,
                    hue,
                    0f
                );
            }

            _count = 0;
        }

        private static void DrawDefined(
            UltimaBatcher2D batcher,
            ref LightSource l,
            Texture2D texture,
            Rectangle uv,
            Vector3 hue,
            in LightDrawOptions options
        )
        {
            float intensity = l.Definition.Intensity;
            float scale = l.Definition.Radius;

            if (options.Flicker)
            {
                LightFlicker.Sample(l.Definition.Flicker, options.Ticks, l.Seed, out float flickerIntensity, out float flickerScale);
                intensity *= flickerIntensity;
                scale *= flickerScale;
            }

            // the hue alpha multiplies the light color; above 1 it would mean "circle of transparency"
            hue.Z = Math.Clamp(intensity, 0f, 1f);

            if (hue.Z <= 0.004f)
            {
                return;
            }

            if (l.Definition.Shape == LightShape.Round && options.RoundTexture != null)
            {
                int size = (int)(Math.Max(uv.Width, uv.Height) * scale);

                batcher.Draw(
                    options.RoundTexture,
                    new Rectangle(l.DrawX - size / 2, l.DrawY - size / 2, size, size),
                    hue,
                    0f
                );

                return;
            }

            batcher.Draw(
                texture,
                new Vector2(l.DrawX, l.DrawY),
                uv,
                hue,
                0f,
                new Vector2(uv.Width * 0.5f, uv.Height * 0.5f),
                new Vector2(scale, scale),
                SpriteEffects.None,
                0f
            );
        }

        // A light is hidden when an opaque static or multi sits on the tile south-east of it,
        // above the light and below the current draw z limit (e.g. a roof over the light).
        private bool IsOccluded(GameObject obj, int maxZ)
        {
            GameObject tile = _world.Map.GetTile(obj.X + 1, obj.Y + 1);

            if (tile == null)
            {
                return false;
            }

            sbyte z5 = (sbyte)(obj.Z + 5);

            for (GameObject o = tile; o != null; o = o.TNext)
            {
                if (
                    (!(o is Static s) || s.ItemData.IsTransparent)
                        && (!(o is Multi m) || m.ItemData.IsTransparent)
                    || !o.AllowedToDraw
                )
                {
                    continue;
                }

                if (o.Z < maxZ && o.Z >= z5)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
