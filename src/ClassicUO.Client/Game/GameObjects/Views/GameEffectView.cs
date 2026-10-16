using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Lighting;
using ClassicUO.Game.Scenes;
using ClassicUO.Assets;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.GameObjects
{
    partial class GameEffect
    {
        private static readonly Lazy<BlendState> _multiplyBlendState = new Lazy<BlendState>
        (
            () =>
            {
                BlendState state = new BlendState
                {
                    ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.Zero,
                    ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.SourceColor
                };

                return state;
            }
        );

        private static readonly Lazy<BlendState> _screenBlendState = new Lazy<BlendState>
        (
            () =>
            {
                BlendState state = new BlendState
                {
                    ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
                    ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.One
                };

                return state;
            }
        );

        private static readonly Lazy<BlendState> _screenLessBlendState = new Lazy<BlendState>
        (
            () =>
            {
                BlendState state = new BlendState
                {
                    ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.DestinationColor,
                    ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.InverseSourceAlpha
                };

                return state;
            }
        );

        private static readonly Lazy<BlendState> _normalHalfBlendState = new Lazy<BlendState>
        (
            () =>
            {
                BlendState state = new BlendState
                {
                    ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.DestinationColor,
                    ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.SourceColor
                };

                return state;
            }
        );

        private static readonly Lazy<BlendState> _shadowBlueBlendState = new Lazy<BlendState>
        (
            () =>
            {
                BlendState state = new BlendState
                {
                    ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.SourceColor,
                    ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.InverseSourceColor,
                    ColorBlendFunction = BlendFunction.ReverseSubtract
                };

                return state;
            }
        );


        // enhanced effects (docs/EFFECTS_PLAN.md): trail of a moving effect, world pixels, newest first
        private const int TRAIL_LENGTH = 4;
        private const uint TRAIL_STEP_MS = 30;
        private readonly Vector2[] _trail = new Vector2[TRAIL_LENGTH];
        private int _trailCount;
        private uint _nextTrailTime;

        public override bool Draw(UltimaBatcher2D batcher, int posX, int posY, float depth)
        {
            if (IsDestroyed || !AllowedToDraw)
            {
                return false;
            }

            if (AnimationGraphic == 0xFFFF)
            {
                return false;
            }

            ref StaticTiles data = ref Client.Game.UO.FileManager.TileData.StaticData[Graphic];

            posX += (int)Offset.X;
            posY += (int)(Offset.Z + Offset.Y);

            ushort hue = Hue;

            if (ProfileManager.CurrentProfile.NoColorObjectsOutOfRange && Distance > World.ClientViewRange)
            {
                hue = Constants.OUT_RANGE_COLOR;
            }
            else if (World.Player.IsDead && ProfileManager.CurrentProfile.EnableBlackWhiteEffect)
            {
                hue = Constants.DEAD_RANGE_COLOR;
            }

            Vector3 hueVec = ShaderHueTranslator.GetHueVector(hue, data.IsPartialHue, data.IsTranslucent ? .5f : 1f, effect: true);

            if (Source != null)
            {
                depth = Source.CalculateDepthZ() + 1f;
            }

            if (ProfileManager.CurrentProfile.EnhancedEffects)
            {
                DrawEnhancements(batcher, posX, posY, hueVec, depth);
            }

            switch (Blend)
            {
                case GraphicEffectBlendMode.Multiply:
                    batcher.SetBlendState(_multiplyBlendState.Value);

                    DrawStaticRotated
                    (
                        batcher,
                        AnimationGraphic,
                        posX,
                        posY,
                        AngleToTarget,
                        hueVec,
                        depth
                    );

                    batcher.SetBlendState(null);

                    break;

                case GraphicEffectBlendMode.Screen:
                case GraphicEffectBlendMode.ScreenMore:
                    batcher.SetBlendState(_screenBlendState.Value);

                    DrawStaticRotated
                    (
                        batcher,
                        AnimationGraphic,
                        posX,
                        posY,
                        AngleToTarget,
                        hueVec,
                        depth
                    );

                    batcher.SetBlendState(null);

                    break;

                case GraphicEffectBlendMode.ScreenLess:
                    batcher.SetBlendState(_screenLessBlendState.Value);

                    DrawStaticRotated
                    (
                        batcher,
                        AnimationGraphic,
                        posX,
                        posY,
                        AngleToTarget,
                        hueVec,
                        depth
                    );

                    batcher.SetBlendState(null);

                    break;

                case GraphicEffectBlendMode.NormalHalfTransparent:
                    batcher.SetBlendState(_normalHalfBlendState.Value);

                    DrawStaticRotated
                    (
                        batcher,
                        AnimationGraphic,
                        posX,
                        posY,
                        AngleToTarget,
                        hueVec,
                        depth
                    );

                    batcher.SetBlendState(null);

                    break;

                case GraphicEffectBlendMode.ShadowBlue:
                    batcher.SetBlendState(_shadowBlueBlendState.Value);

                    DrawStaticRotated
                    (
                        batcher,
                        AnimationGraphic,
                        posX,
                        posY,
                        AngleToTarget,
                        hueVec,
                        depth
                    );

                    batcher.SetBlendState(null);

                    break;

                default:
                    //if (Graphic == 0x36BD)
                    //{
                    //    hueVector = ShaderHueTranslator.GetHueVector(0);
                    //    HueVector.X = 0;
                    //    HueVector.Y = ShaderHueTranslator.SHADER_LIGHTS;
                    //    HueVector.Z = 0;
                    //    batcher.SetBlendState(BlendState.Additive);
                    //    base.Draw(batcher, posX, posY);
                    //    batcher.SetBlendState(null);
                    //}
                    //else

                    DrawStaticRotated
                    (
                        batcher,
                        AnimationGraphic,
                        posX,
                        posY,
                        AngleToTarget,
                        hueVec,
                        depth
                    );

                    break;
            }

            //Engine.DebugInfo.EffectsRendered++;


            if (data.IsLight && Source != null)
            {
                Client.Game.GetScene<GameScene>().AddLight(Source, Source, posX + 22, posY + 22);
            }

            return true;
        }

        // Light around the effect in its own color, and a fading trail behind moving effects
        private void DrawEnhancements(UltimaBatcher2D batcher, int posX, int posY, Vector3 hueVec, float depth)
        {
            Vector3 color = EffectLights.GetColor(AnimationGraphic);

            if (color != Vector3.Zero && Blend != GraphicEffectBlendMode.Multiply && Blend != GraphicEffectBlendMode.ShadowBlue)
            {
                ref readonly var art = ref Client.Game.UO.Arts.GetArt(AnimationGraphic);
                float radius = Math.Clamp(Math.Max(art.UV.Width, art.UV.Height) * 1.6f, 60f, 200f);

                EffectLights.Add(new Vector2(posX + 22, posY + 22), radius, color * 0.85f);
            }

            if (!IsMoving)
            {
                return;
            }

            Point offset = GameScene.DrawOffset;
            Vector2 absolute = new Vector2(posX + offset.X, posY + offset.Y);

            if (Time.Ticks >= _nextTrailTime)
            {
                _nextTrailTime = Time.Ticks + TRAIL_STEP_MS;

                for (int i = Math.Min(_trailCount, TRAIL_LENGTH - 1); i > 0; i--)
                {
                    _trail[i] = _trail[i - 1];
                }

                _trail[0] = absolute;
                _trailCount = Math.Min(_trailCount + 1, TRAIL_LENGTH);
            }

            // additive copies, fainter further back; skip the newest (where the effect is now)
            batcher.SetBlendState(_screenBlendState.Value);

            for (int i = 1; i < _trailCount; i++)
            {
                Vector3 trailHue = hueVec;
                trailHue.Z = 0.4f * (1f - i / (float)TRAIL_LENGTH);

                DrawStaticRotated(
                    batcher,
                    AnimationGraphic,
                    (int)(_trail[i].X - offset.X),
                    (int)(_trail[i].Y - offset.Y),
                    AngleToTarget,
                    trailHue,
                    depth - 0.01f * i
                );
            }

            batcher.SetBlendState(null);
        }

        public override bool CheckMouseSelection()
        {
            return false;
        }

    }
}
