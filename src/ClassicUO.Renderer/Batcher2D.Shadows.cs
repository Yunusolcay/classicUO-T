// SPDX-License-Identifier: BSD-2-Clause

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    // Soft shadows (docs/SHADOWS_PLAN.md, S1). While deferred, DrawShadow collects the shadow sprites
    // instead of drawing them in the world queue. DrawDeferredShadows then draws them all at once:
    //  1. the target alpha is cleared to 0,
    //  2. every shadow writes its coverage to the alpha channel with a max blend, depth tested
    //     against the world (objects in front of a shadow still hide it, as in the queue),
    //  3. the color is multiplied by 1 - alpha, so overlapping shadows darken only once,
    //  4. the alpha is set back to 1.
    // The shader softens the shadow edges more towards the far end of the shadow.
    public sealed unsafe partial class UltimaBatcher2D
    {
        private static readonly BlendState _clearAlpha = new BlendState
        {
            ColorWriteChannels = ColorWriteChannels.Alpha,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.Zero
        };

        private static readonly BlendState _maxAlpha = new BlendState
        {
            ColorWriteChannels = ColorWriteChannels.Alpha,
            AlphaBlendFunction = BlendFunction.Max,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One
        };

        // color = color * (1 - destination alpha)
        private static readonly BlendState _darkenByAlpha = new BlendState
        {
            ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue,
            ColorSourceBlend = Blend.Zero,
            ColorDestinationBlend = Blend.InverseDestinationAlpha
        };

        private static readonly BlendState _restoreAlpha = new BlendState
        {
            ColorWriteChannels = ColorWriteChannels.Alpha,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.Zero
        };

        private static readonly DepthStencilState _shadowDepth = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false,
            DepthBufferFunction = CompareFunction.LessEqual,
            StencilEnable = false
        };

        private PositionNormalTextureColor4[] _shadowVertices = new PositionNormalTextureColor4[256];
        private Texture2D[] _shadowTextures = new Texture2D[256];
        private int _shadowCount;
        private bool _deferShadows;
        private float _shadowStrength;
        private Texture2D _white;

        // strength: darkness of a full shadow, 0..1. Shadows drawn until DrawDeferredShadows are soft.
        public void BeginDeferredShadows(float strength)
        {
            _deferShadows = true;
            _shadowStrength = Math.Clamp(strength, 0f, 1f);
            _shadowCount = 0;
        }

        // Draws the collected shadows over what is in the current target (see the class comment) and
        // returns to drawing shadows in the queue. targetWidth/Height: size of the bound target.
        public void DrawDeferredShadows(int targetWidth, int targetHeight)
        {
            _deferShadows = false;

            if (_shadowCount == 0)
            {
                return;
            }

            EnsureStarted();

            DepthStencilState previousStencil = _stencil;
            BlendState previousBlend = _blendState;
            Matrix previousTransform = _transformMatrix;

            Texture2D white = GetWhiteTexture();

            // full screen passes are in target pixels
            SetStencil(null);
            _transformMatrix = Matrix.Identity;
            Rectangle full = new Rectangle(0, 0, targetWidth, targetHeight);

            SetBlendState(_clearAlpha);
            Draw(white, full, Vector3.UnitZ, 0f);

            // shadows use the world transform and depth
            Flush();
            _transformMatrix = previousTransform;
            SetBlendState(_maxAlpha);
            SetStencil(_shadowDepth);

            for (int i = 0; i < _shadowCount; i++)
            {
                EnsureSize();
                _vertexInfo[_numSprites] = _shadowVertices[i];
                PushSprite(_shadowTextures[i]);
                _shadowTextures[i] = null;
            }

            Flush();
            _transformMatrix = Matrix.Identity;
            SetStencil(null);

            SetBlendState(_darkenByAlpha);
            Draw(white, full, Vector3.UnitZ, 0f);

            SetBlendState(_restoreAlpha);
            Draw(white, full, Vector3.UnitZ, 0f);

            Flush();
            _transformMatrix = previousTransform;
            SetBlendState(previousBlend);
            SetStencil(previousStencil);

            _shadowCount = 0;
        }

        // Called by DrawShadow with the finished sprite: marks it soft and keeps it for later
        private void DeferShadow(ref PositionNormalTextureColor4 vertex, Texture2D texture, float minU, float minV, float maxU, float maxV)
        {
            if (texture == null || texture.IsDisposed || _shadowStrength <= 0f)
            {
                return;
            }

            // soft shadow data for the shader: sprite bounds in Normal (u min, v min, u max) and
            // Hue (x = strength, z = v max); the shader keeps its samples inside the sprite
            Vector3 bounds = new Vector3(minU, minV, maxU);
            vertex.Normal0 = vertex.Normal1 = vertex.Normal2 = vertex.Normal3 = bounds;
            vertex.Hue0.X = vertex.Hue1.X = vertex.Hue2.X = vertex.Hue3.X = _shadowStrength;
            vertex.Hue0.Z = vertex.Hue1.Z = vertex.Hue2.Z = vertex.Hue3.Z = maxV;

            if (_shadowCount >= _shadowVertices.Length)
            {
                Array.Resize(ref _shadowVertices, _shadowVertices.Length * 2);
                Array.Resize(ref _shadowTextures, _shadowTextures.Length * 2);
            }

            _shadowVertices[_shadowCount] = vertex;
            _shadowTextures[_shadowCount] = texture;
            _shadowCount++;
        }

        private Texture2D GetWhiteTexture()
        {
            if (_white == null || _white.IsDisposed)
            {
                _white = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _white.SetData(new[] { Color.White });
            }

            return _white;
        }
    }
}
