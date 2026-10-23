// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using System;
using MathHelper = Microsoft.Xna.Framework.MathHelper;

namespace ClassicUO.Game
{
    enum WeatherType
    {
        WT_RAIN = 0,
        WT_STORM_APPROACH,
        WT_SNOW,
        WT_STORM_BREWING,

        WT_INVALID_0 = 0xFE,
        WT_INVALID_1 = 0xFF
    }

    internal sealed class Weather
    {
        private const int MAX_WEATHER_EFFECT = 70;
        private const float SIMULATION_TIME = 37.0f;

        private readonly WeatherEffect[] _effects = new WeatherEffect[byte.MaxValue];
        private uint _timer, _windTimer, _lastTick;
        private readonly World _world;
        // enhanced weather drawing (docs/WATER_WEATHER_PLAN.md); this class still runs the server weather
        private readonly WeatherRenderer _renderer = new WeatherRenderer();

        public Weather(World world)
        {
            _world = world;
            _isWater = IsWaterAt;
        }

        // rain impacts look different on water and on land
        private readonly Func<Vector2, bool> _isWater;


        public WeatherType? CurrentWeather { get; private set; }
        public WeatherType Type { get; private set; }
        public byte Count { get; private set; }
        public byte ScaledCount { get; private set; }
        public byte CurrentCount { get; private set; }
        public byte Temperature{ get; private set; }
        public sbyte Wind { get; private set; }



        private static float SinOscillate(float freq, int range, uint current_tick)
        {
            float anglef = (int) (current_tick / 2.7777f * freq) % 360;

            return Math.Sign(MathHelper.ToRadians(anglef)) * range;
        }


        public void Reset()
        {
            Type = 0;
            Count = CurrentCount = Temperature = 0;
            Wind = 0;
            _windTimer = _timer = 0;
            CurrentWeather = null;
            _renderer.Reset();
        }

        public void Generate(WeatherType type, byte count, byte temp)
        {
            bool extended = CurrentWeather.HasValue && CurrentWeather == type;

            if (!extended)
            {
                Reset();
            }

            Type = type;
            Count = (byte) Math.Min(MAX_WEATHER_EFFECT, (int) count);
            Temperature = temp;
            _timer = Time.Ticks + Constants.WEATHER_TIMER;

            _lastTick = Time.Ticks;

            if (Type == WeatherType.WT_INVALID_0 || Type == WeatherType.WT_INVALID_1)
            {
                _timer = 0;
                CurrentWeather = null;

                return;
            }

            bool showMessage = Count > 0 && !extended;

            switch (type)
            {
                case WeatherType.WT_RAIN:
                    if (showMessage)
                    {
                        GameActions.Print
                        (
                            _world,
                            ResGeneral.ItBeginsToRain,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );

                        CurrentWeather = type;
                    }

                    break;

                case WeatherType.WT_STORM_APPROACH:
                    if (showMessage)
                    {
                        GameActions.Print
                        (
                            _world,
                            ResGeneral.AFierceStormApproaches,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );

                        CurrentWeather = type;

                        PlayThunder();
                    }

                    break;

                case WeatherType.WT_SNOW:
                    if (showMessage)
                    {
                        GameActions.Print
                        (
                            _world,
                            ResGeneral.ItBeginsToSnow,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );

                        CurrentWeather = type;

                        PlayWind();
                    }

                    break;

                case WeatherType.WT_STORM_BREWING:
                    if (showMessage)
                    {
                        GameActions.Print
                        (
                            _world,
                            ResGeneral.AStormIsBrewing,
                            1154,
                            MessageType.System,
                            3,
                            false
                        );

                        CurrentWeather = type;

                        PlayThunder();
                    }

                    break;
            }


            _windTimer = 0;

            ScaledCount = CalculateScaledCount(Count);
            CurrentCount = ScaledCount;

            for (int i = 0; i < _effects.Length; i++)
            {
                ref WeatherEffect effect = ref _effects[i];
                effect.X = RandomHelper.GetValue(0, Client.Game.Scene.Camera.Bounds.Width);
                effect.Y = RandomHelper.GetValue(0, Client.Game.Scene.Camera.Bounds.Height);
            }
        }

        private static byte CalculateScaledCount(byte count)
        {
            if (count <= 0)
            {
                return 0;
            }
            float legacyWindowSize = 640 * 480;
            return (byte)Math.Max(1, Math.Min(byte.MaxValue, count * (Client.Game.Scene.Camera.Bounds.Width * Client.Game.Scene.Camera.Bounds.Height) / legacyWindowSize));
        }

        private void PlayWind()
        {
            PlaySound(RandomHelper.RandomList(0x014, 0x015, 0x016));
        }

        private void PlayThunder()
        {
           PlaySound(RandomHelper.RandomList(0x028, 0x206));
           _renderer.TriggerLightning();
        }

        private void PlaySound(int sound)
        {
            // randomize the sound of the weather around the player
            int randX = RandomHelper.GetValue(10, 18);
            if (RandomHelper.RandomBool())
            {
                randX *= -1;
            }

            int randY = RandomHelper.GetValue(10, 18);
            if (RandomHelper.RandomBool())
            {
                randY *= -1;
            }

            Client.Game.Audio.PlaySoundWithDistance(_world, sound, _world.Player.X + randX, _world.Player.Y + randY);
        }

        public void Draw(UltimaBatcher2D batcher, int x, int y, float layerDepth)
        {
            bool removeEffects = false;

            if (_timer < Time.Ticks)
            {
                if (CurrentCount == 0)
                {
                    // Time for the weather has passed and all weather effects have disappeared
                    Reset();
                    return;
                }

                removeEffects = true;
            }
            else if (Type == WeatherType.WT_INVALID_0 || Type == WeatherType.WT_INVALID_1)
            {
                return;
            }

            //Rescale the count if window size has changed
            byte newScaledCount = CalculateScaledCount(Count);

            if (newScaledCount != ScaledCount)
            {
                CurrentCount = (byte)Math.Min(byte.MaxValue, CurrentCount * newScaledCount / ScaledCount);
                ScaledCount = newScaledCount;
            }

            uint passed = Time.Ticks - _lastTick;

            if (passed > 7000)
            {
                _lastTick = Time.Ticks;
                passed = 25;
            }

            bool windChanged = false;

            if (_windTimer < Time.Ticks)
            {
                if (_windTimer == 0)
                {
                    windChanged = true;
                }

                _windTimer = Time.Ticks + (uint) (RandomHelper.GetValue(13, 19) * 1000);

                sbyte lastWind = Wind;

                Wind = (sbyte) RandomHelper.GetValue(0, 4);

                if (RandomHelper.GetValue(0, 2) != 0)
                {
                    Wind *= -1;
                }

                if (Wind < 0 && lastWind > 0)
                {
                    Wind = 0;
                }
                else if (Wind > 0 && lastWind < 0)
                {
                    Wind = 0;
                }

                if (lastWind != Wind)
                {
                    windChanged = true;
                }
            }

            //switch ((WEATHER_TYPE) Type)
            //{
            //    case WEATHER_TYPE.WT_RAIN:
            //    case WEATHER_TYPE.WT_FIERCE_STORM:
            //        // TODO: set color
            //        break;
            //    case WEATHER_TYPE.WT_SNOW:
            //    case WEATHER_TYPE.WT_STORM:
            //        // TODO: set color
            //        break;
            //    default:
            //        break;
            //}

            Profile profile = ProfileManager.CurrentProfile;

            if (profile != null && profile.EnhancedWeather)
            {
                DrawEnhanced(batcher, profile, removeEffects, windChanged, layerDepth);

                return;
            }

            //Point winpos = ProfileManager.CurrentProfile.GameWindowPosition;
            Point winsize = new Point(Client.Game.Scene.Camera.Bounds.Width, Client.Game.Scene.Camera.Bounds.Height);

            Rectangle snowRect = new Rectangle(0, 0, 2, 2);

            for (int i = 0; i < CurrentCount; i++)
            {
                ref WeatherEffect effect = ref _effects[i];

                if (effect.X < x || effect.X > x + winsize.X || effect.Y < y || effect.Y > y + winsize.Y)
                {
                    if (removeEffects)
                    {
                        if (CurrentCount > 0)
                        {
                            CurrentCount--;
                        }
                        else
                        {
                            CurrentCount = 0;
                        }

                        continue;
                    }

                    effect.X = x + RandomHelper.GetValue(0, winsize.X);
                    effect.Y = y + RandomHelper.GetValue(0, winsize.Y);
                }


                switch (Type)
                {
                    case WeatherType.WT_RAIN:
                        float scaleRation = effect.ScaleRatio;
                        effect.SpeedX = -4.5f - scaleRation;
                        effect.SpeedY = 5.0f + scaleRation;

                        break;

                    case WeatherType.WT_STORM_BREWING:
                        effect.SpeedX = Wind * 1.5f;
                        effect.SpeedY = 1.5f;

                        if (windChanged)
                        {
                            PlayThunder();
                        }

                        break;

                    case WeatherType.WT_SNOW:
                    case WeatherType.WT_STORM_APPROACH:

                        if (Type == WeatherType.WT_SNOW)
                        {
                            effect.SpeedX = Wind;
                            effect.SpeedY = 1.0f;
                        }
                        else
                        {
                            effect.SpeedX = Wind;
                            effect.SpeedY = 6.0f;
                        }

                        if (windChanged)
                        {
                            effect.SpeedAngle = MathHelper.ToDegrees((float) Math.Atan2(effect.SpeedX, effect.SpeedY));

                            effect.SpeedMagnitude = (float) Math.Sqrt(Math.Pow(effect.SpeedX, 2) + Math.Pow(effect.SpeedY, 2));

                            if (Type == WeatherType.WT_SNOW)
                            {
                                PlayWind();
                            }
                            else
                            {
                                PlayThunder();
                            }
                        }

                        float speedAngle = effect.SpeedAngle;
                        float speedMagnitude = effect.SpeedMagnitude;

                        speedMagnitude += effect.ScaleRatio;

                        speedAngle += SinOscillate(0.4f, 20, Time.Ticks + effect.ID);

                        float rad = MathHelper.ToRadians(speedAngle);
                        effect.SpeedX = speedMagnitude * (float) Math.Sin(rad);
                        effect.SpeedY = speedMagnitude * (float) Math.Cos(rad);

                        break;
                }

                float speedOffset = passed / SIMULATION_TIME;

                switch (Type)
                {
                    case WeatherType.WT_RAIN:
                    case WeatherType.WT_STORM_APPROACH:

                        int oldX = (int) effect.X;
                        int oldY = (int) effect.Y;

                        float ofsx = effect.SpeedX * speedOffset;
                        float ofsy = effect.SpeedY * speedOffset;

                        effect.X += ofsx;
                        effect.Y += ofsy;

                        const float MAX_OFFSET_XY = 5.0f;

                        if (ofsx >= MAX_OFFSET_XY)
                        {
                            oldX = (int) (effect.X - MAX_OFFSET_XY);
                        }
                        else if (ofsx <= -MAX_OFFSET_XY)
                        {
                            oldX = (int) (effect.X + MAX_OFFSET_XY);
                        }

                        if (ofsy >= MAX_OFFSET_XY)
                        {
                            oldY = (int) (effect.Y - MAX_OFFSET_XY);
                        }
                        else if (oldY <= -MAX_OFFSET_XY)
                        {
                            oldY = (int) (effect.Y + MAX_OFFSET_XY);
                        }

                        Vector2 start = new Vector2(x + oldX, y + oldY);
                        Vector2 end = new Vector2(x + effect.X, y + effect.Y);

                        batcher.DrawLine
                        (
                           SolidColorTextureCache.GetTexture(Color.Blue),
                           start,
                           end,
                           Vector3.UnitZ,
                           2,
                           layerDepth
                        );

                        break;

                    case WeatherType.WT_SNOW:

                        effect.X += effect.SpeedX * speedOffset;
                        effect.Y += effect.SpeedY * speedOffset;

                        snowRect.X = x + (int) effect.X;
                        snowRect.Y = y + (int) effect.Y;

                        batcher.Draw
                        (
                            SolidColorTextureCache.GetTexture(Color.White),
                            snowRect,
                            Vector3.UnitZ,
                            layerDepth
                        );

                        break;
                }
            }

            _lastTick = Time.Ticks;
        }


        // Same weather rules (type, count, duration, wind, sounds), drawn by WeatherRenderer in the
        // visible part of the world whatever the zoom
        private void DrawEnhanced(UltimaBatcher2D batcher, Profile profile, bool ending, bool windChanged, float layerDepth)
        {
            if (windChanged)
            {
                switch (Type)
                {
                    case WeatherType.WT_SNOW:
                        PlayWind();

                        break;

                    case WeatherType.WT_STORM_APPROACH:
                    case WeatherType.WT_STORM_BREWING:
                        PlayThunder();

                        break;
                }
            }

            Camera camera = Client.Game.Scene.Camera;
            Point origin = camera.ScreenToWorld(Point.Zero);

            _renderer.Draw(
                batcher,
                Type,
                Count,
                Wind,
                !ending,
                profile.LightningFlashes,
                new Vector2(origin.X, origin.Y),
                camera.Zoom,
                camera.Bounds.Width,
                camera.Bounds.Height,
                layerDepth,
                Time.Delta,
                _isWater
            );

            // the server weather ran out and the particles faded: the next Draw resets the weather
            if (ending && _renderer.Intensity <= 0f)
            {
                CurrentCount = 0;
            }

            _lastTick = Time.Ticks;
        }


        // world: a pixel of the view in the scene's draw space (as GameObject.RealScreenPosition).
        // Finds the tile drawn there, assuming the ground near the player's height, and tells
        // whether it is water.
        private bool IsWaterAt(Vector2 world)
        {
            Map.Map map = _world.Map;

            if (map == null || _world.Player == null)
            {
                return false;
            }

            Point offset = Scenes.GameScene.DrawOffset;
            // tile centre: x = (X - Y) * 22, y = (X + Y) * 22 - Z * 4 (minus the draw offset)
            float diff = (world.X + offset.X) / 22f;
            int z = _world.Player.Z;
            int x = 0, y = 0;

            for (int i = 0; i < 2; i++)
            {
                float sum = (world.Y + offset.Y + z * 4) / 22f;
                x = (int)Math.Round((sum + diff) * 0.5f);
                y = (int)Math.Round((sum - diff) * 0.5f);

                if (x < 0 || y < 0)
                {
                    return false;
                }

                // once more with the height of the tile found
                z = map.GetTileZ(x, y);

                if (z == -125)
                {
                    return false;
                }
            }

            for (GameObject obj = map.GetTile(x, y, false); obj != null; obj = obj.TNext)
            {
                if (obj is Land land && land.TileData.IsWet || obj is Static st && st.ItemData.IsWet)
                {
                    return true;
                }
            }

            return false;
        }

        private struct WeatherEffect
        {
            public float SpeedX, SpeedY, X, Y, ScaleRatio, SpeedAngle, SpeedMagnitude;
            public uint ID;
        }
    }
}