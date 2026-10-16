// SPDX-License-Identifier: BSD-2-Clause

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    // A light that casts soft shadows at night, in world render target pixels (docs/SHADOWS_PLAN.md, S3)
    public struct ShadowLight
    {
        public Vector2 Position;
        // casters farther than this get no shadow from the light
        public float Radius;
        // 0..1
        public float Strength;
    }

    // Soft shadows (docs/SHADOWS_PLAN.md). While deferred, DrawShadow and DrawContactShadow collect
    // shadow casters instead of drawing in the world queue. DrawDeferredShadows then draws them all:
    //  1. the target alpha is cleared to 0,
    //  2. every shadow writes its coverage to the alpha channel with a max blend, depth tested
    //     against the world with "less": the caster itself (same depth) is never darkened, objects
    //     in front still hide the shadow,
    //  3. the color is multiplied by 1 - alpha, so overlapping shadows darken only once,
    //  4. the alpha is set back to 1.
    // Casters get a sun shadow (fixed UO direction, fading at night) and shadows away from the
    // strongest nearby lights (S3). The shader softens the edges more towards the far end.
    public sealed unsafe partial class UltimaBatcher2D
    {
        // texels added around the sprite so the soft edge has room outside the silhouette (S2)
        private const int SHADOW_MARGIN = 3;
        // local light shadows per caster
        private const int MAX_LIGHTS_PER_CASTER = 2;
        // contact shadow ellipse under a mobile (S4)
        private const int CONTACT_WIDTH = 34;
        private const int CONTACT_HEIGHT = 14;
        private const int CONTACT_TEXTURE_SIZE = 64;

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
            DepthBufferFunction = CompareFunction.Less,
            StencilEnable = false
        };

        private struct ShadowCaster
        {
            public Texture2D Texture;
            // UVs of the sprite grown by the margin, and of the sprite itself (sampling bounds)
            public float U, V, UW, VH;
            public Vector4 Bounds;
            // screen: left end of the feet line, width of the grown sprite, half its height
            public Vector2 BaseLeft;
            public float Width, Height;
            public float Depth;
            public bool Flip;
            // true: contact ellipse, only darkened by its own strength
            public bool Contact;
        }

        private ShadowCaster[] _casters = new ShadowCaster[256];
        private int _casterCount;
        private PositionNormalTextureColor4[] _shadowVertices = new PositionNormalTextureColor4[512];
        private Texture2D[] _shadowTextures = new Texture2D[512];
        private int _shadowCount;
        private bool _deferShadows;
        private float _sunStrength, _contactStrength;
        private Texture2D _white, _contactTexture;

        // shadows drawn by the last DrawDeferredShadows, for the debug overlay (S5)
        public int LastShadowCount { get; private set; }

        // sunStrength: darkness of a full sun shadow, contactStrength: of the contact ellipse (0..1).
        // Shadows drawn until DrawDeferredShadows are collected.
        public void BeginDeferredShadows(float sunStrength, float contactStrength)
        {
            _deferShadows = true;
            _sunStrength = Math.Clamp(sunStrength, 0f, 1f);
            _contactStrength = Math.Clamp(contactStrength, 0f, 1f);
            _casterCount = 0;
        }

        // Soft dark ellipse under a mobile's feet (x, y: feet center). Only while shadows are deferred.
        public void DrawContactShadow(float x, float y, float depth)
        {
            if (!_deferShadows || _contactStrength <= 0f)
            {
                return;
            }

            Texture2D texture = GetContactTexture();
            float half = 0.5f / CONTACT_TEXTURE_SIZE;
            x += SpriteOffset.X;
            y += SpriteOffset.Y;

            ref ShadowCaster caster = ref AddCaster();
            caster.Texture = texture;
            caster.U = half;
            caster.V = half;
            caster.UW = 1f - 2f * half;
            caster.VH = 1f - 2f * half;
            caster.Bounds = new Vector4(half, half, 1f - half, 1f - half);
            caster.BaseLeft = new Vector2(x - CONTACT_WIDTH * 0.5f, y + CONTACT_HEIGHT * 0.5f);
            caster.Width = CONTACT_WIDTH;
            caster.Height = CONTACT_HEIGHT;
            caster.Depth = depth;
            caster.Flip = false;
            caster.Contact = true;
        }

        // Draws the collected shadows over what is in the current target (see the class comment) and
        // returns to drawing shadows in the queue. localStrength: darkness of a full light shadow.
        public void DrawDeferredShadows(int targetWidth, int targetHeight, ReadOnlySpan<ShadowLight> lights, float localStrength)
        {
            _deferShadows = false;
            _shadowCount = 0;

            for (int i = 0; i < _casterCount; i++)
            {
                AddShadows(ref _casters[i], lights, localStrength);
                _casters[i].Texture = null;
            }

            _casterCount = 0;
            LastShadowCount = _shadowCount;

            if (_shadowCount == 0)
            {
                return;
            }

            EnsureStarted();

            DepthStencilState previousStencil = _stencil;
            BlendState previousBlend = _blendState;
            Matrix previousTransform = _transformMatrix;

            Texture2D white = GetWhiteTexture();
            Rectangle full = new Rectangle(0, 0, targetWidth, targetHeight);

            // full screen passes are in target pixels
            SetStencil(null);
            _transformMatrix = Matrix.Identity;

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

        // DrawShadow while deferred: keeps the caster, the shadows are made in DrawDeferredShadows
        private void DeferShadowCaster(Texture2D texture, Vector2 position, Rectangle sourceRect, bool flip, float depth)
        {
            if (texture == null || texture.IsDisposed)
            {
                return;
            }

            CalculateHalfPixelUVs(sourceRect, texture.Width, texture.Height, out float bx, out float by, out float bw, out float bh);

            // grown on the sides and the top (head end); the feet line stays where it was
            Rectangle grown = new Rectangle(
                sourceRect.X - SHADOW_MARGIN,
                sourceRect.Y - SHADOW_MARGIN,
                sourceRect.Width + SHADOW_MARGIN * 2,
                sourceRect.Height + SHADOW_MARGIN
            );

            CalculateHalfPixelUVs(grown, texture.Width, texture.Height, out float u, out float v, out float uw, out float vh);

            ref ShadowCaster caster = ref AddCaster();
            caster.Texture = texture;
            caster.U = u;
            caster.V = v;
            caster.UW = uw;
            caster.VH = vh;
            caster.Bounds = new Vector4(bx, by, bx + bw, by + bh);
            // same feet line as the original shadow: sprite bottom - 10
            caster.BaseLeft = new Vector2(position.X - SHADOW_MARGIN, position.Y + sourceRect.Height - 10);
            caster.Width = grown.Width;
            caster.Height = grown.Height * 0.5f;
            caster.Depth = depth;
            caster.Flip = flip;
            caster.Contact = false;
        }

        private void AddShadows(ref ShadowCaster caster, ReadOnlySpan<ShadowLight> lights, float localStrength)
        {
            if (caster.Contact)
            {
                AddShadowQuad(ref caster, new Vector2(0f, -caster.Height), _contactStrength);

                return;
            }

            // original UO sun direction: the top of the sprite leans up and to the right
            if (_sunStrength > 0f)
            {
                AddShadowQuad(ref caster, new Vector2(caster.Height, -caster.Height), _sunStrength);
            }

            if (localStrength <= 0f || lights.IsEmpty)
            {
                return;
            }

            Vector2 feet = caster.BaseLeft + new Vector2(caster.Width * 0.5f, 0f);

            // the strongest lights reaching this caster
            Span<int> best = stackalloc int[MAX_LIGHTS_PER_CASTER];
            Span<float> weight = stackalloc float[MAX_LIGHTS_PER_CASTER];
            best.Fill(-1);
            weight.Clear();

            for (int i = 0; i < lights.Length; i++)
            {
                ref readonly ShadowLight light = ref lights[i];
                float distance = Vector2.Distance(feet, light.Position);

                if (distance >= light.Radius || distance < 4f)
                {
                    continue;
                }

                float w = light.Strength * (1f - distance / light.Radius);

                for (int k = 0; k < MAX_LIGHTS_PER_CASTER; k++)
                {
                    if (w > weight[k])
                    {
                        for (int m = MAX_LIGHTS_PER_CASTER - 1; m > k; m--)
                        {
                            weight[m] = weight[m - 1];
                            best[m] = best[m - 1];
                        }

                        weight[k] = w;
                        best[k] = i;

                        break;
                    }
                }
            }

            for (int k = 0; k < MAX_LIGHTS_PER_CASTER && best[k] >= 0; k++)
            {
                ref readonly ShadowLight light = ref lights[best[k]];
                Vector2 away = feet - light.Position;
                float distance = away.Length();
                // closer lights cast shorter, darker shadows
                float length = caster.Height * Math.Clamp(0.5f + distance / light.Radius, 0.5f, 1.5f);

                AddShadowQuad(ref caster, away / distance * length, localStrength * Math.Min(weight[k] * 1.5f, 1f));
            }
        }

        // Quad from the feet line (bottom of the sprite) to the feet line + offset (top of the sprite)
        private void AddShadowQuad(ref ShadowCaster caster, Vector2 offset, float strength)
        {
            if (strength <= 0.004f)
            {
                return;
            }

            // a shadow lying flat along the feet line would have no area
            if (Math.Abs(offset.Y) < 0.5f)
            {
                offset.Y = -0.5f;
            }

            if (_shadowCount >= _shadowVertices.Length)
            {
                Array.Resize(ref _shadowVertices, _shadowVertices.Length * 2);
                Array.Resize(ref _shadowTextures, _shadowTextures.Length * 2);
            }

            ref PositionNormalTextureColor4 vertex = ref _shadowVertices[_shadowCount];

            Vector2 left = caster.BaseLeft;
            Vector2 right = left + new Vector2(caster.Width, 0f);

            vertex.Position0 = new Vector3(left + offset, caster.Depth);
            vertex.Position1 = new Vector3(right + offset, caster.Depth);
            vertex.Position2 = new Vector3(left, caster.Depth);
            vertex.Position3 = new Vector3(right, caster.Depth);

            byte effects = (byte)((caster.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None) & (SpriteEffects)0x03);

            vertex.TextureCoordinate0 = new Vector3(_cornerOffsetX[0 ^ effects] * caster.UW + caster.U, _cornerOffsetY[0 ^ effects] * caster.VH + caster.V, 0f);
            vertex.TextureCoordinate1 = new Vector3(_cornerOffsetX[1 ^ effects] * caster.UW + caster.U, _cornerOffsetY[1 ^ effects] * caster.VH + caster.V, 0f);
            vertex.TextureCoordinate2 = new Vector3(_cornerOffsetX[2 ^ effects] * caster.UW + caster.U, _cornerOffsetY[2 ^ effects] * caster.VH + caster.V, 0f);
            vertex.TextureCoordinate3 = new Vector3(_cornerOffsetX[3 ^ effects] * caster.UW + caster.U, _cornerOffsetY[3 ^ effects] * caster.VH + caster.V, 0f);

            // soft shadow data for the shader: sprite bounds in Normal (u min, v min, u max) and
            // Hue (x = strength, z = v max); the shader keeps its samples inside the sprite
            Vector3 bounds = new Vector3(caster.Bounds.X, caster.Bounds.Y, caster.Bounds.Z);
            vertex.Normal0 = vertex.Normal1 = vertex.Normal2 = vertex.Normal3 = bounds;
            Vector3 hue = new Vector3(strength, ShaderHueTranslator.SHADER_SHADOW, caster.Bounds.W);
            vertex.Hue0 = vertex.Hue1 = vertex.Hue2 = vertex.Hue3 = hue;

            // a shadow pointing down the screen flips the winding: swap left and right vertices
            // (each keeps its texture coordinate) so the quad is not culled
            if (offset.Y > 0f)
            {
                (vertex.Position0, vertex.Position1) = (vertex.Position1, vertex.Position0);
                (vertex.TextureCoordinate0, vertex.TextureCoordinate1) = (vertex.TextureCoordinate1, vertex.TextureCoordinate0);
                (vertex.Position2, vertex.Position3) = (vertex.Position3, vertex.Position2);
                (vertex.TextureCoordinate2, vertex.TextureCoordinate3) = (vertex.TextureCoordinate3, vertex.TextureCoordinate2);
            }

            _shadowTextures[_shadowCount] = caster.Texture;
            _shadowCount++;
        }

        private ref ShadowCaster AddCaster()
        {
            if (_casterCount >= _casters.Length)
            {
                Array.Resize(ref _casters, _casters.Length * 2);
            }

            return ref _casters[_casterCount++];
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

        // round alpha falloff: full in the middle, zero at the edge
        private Texture2D GetContactTexture()
        {
            if (_contactTexture == null || _contactTexture.IsDisposed)
            {
                int size = CONTACT_TEXTURE_SIZE;
                Color[] pixels = new Color[size * size];
                float half = size * 0.5f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - half) / half;
                        float dy = (y + 0.5f - half) / half;
                        float falloff = 1f - Math.Clamp(dx * dx + dy * dy, 0f, 1f);

                        pixels[y * size + x] = new Color(0, 0, 0, (byte)(falloff * falloff * 255f + 0.5f));
                    }
                }

                _contactTexture = new Texture2D(GraphicsDevice, size, size, false, SurfaceFormat.Color);
                _contactTexture.SetData(pixels);
            }

            return _contactTexture;
        }
    }
}
