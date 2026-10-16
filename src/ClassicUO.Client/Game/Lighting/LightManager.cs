// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Lighting
{
    // Collects the light sources of a frame and draws them into the light map.
    // Behavior matches the original GameScene light code.
    internal sealed class LightManager
    {
        private readonly World _world;
        private readonly LightSource[] _lights = new LightSource[LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT];
        private int _count;

        public LightManager(World world)
        {
            _world = world;
        }

        public int Count => _count;

        // maxZ: the scene's current draw z limit; lights under a roof above it are skipped
        public void Add(GameObject obj, GameObject lightObject, int x, int y, int maxZ)
        {
            if (_count >= LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT || obj == null)
            {
                return;
            }

            if (IsOccluded(obj, maxZ))
            {
                return;
            }

            ref LightSource light = ref _lights[_count];

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

            if (light.ID >= LightsLoader.MAX_LIGHTS_DATA_INDEX_COUNT)
            {
                return;
            }

            if (light.Color != 0)
            {
                light.Color++;
            }

            light.DrawX = x;
            light.DrawY = y;
            _count++;
        }

        // Draws the collected lights with the batcher already begun on the light render target,
        // then starts a new frame of collection.
        public void Draw(UltimaBatcher2D batcher)
        {
            Vector3 hue = Vector3.Zero;

            hue.Z = 1f;

            for (int i = 0; i < _count; i++)
            {
                ref LightSource l = ref _lights[i];
                ref readonly var lightInfo = ref Client.Game.UO.Lights.GetLight(l.ID);

                if (lightInfo.Texture == null)
                {
                    continue;
                }

                hue.X = l.Color;
                hue.Y =
                    hue.X > 1.0f
                        ? l.IsHue
                            ? ShaderHueTranslator.SHADER_HUED
                            : ShaderHueTranslator.SHADER_LIGHTS
                        : ShaderHueTranslator.SHADER_NONE;

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
