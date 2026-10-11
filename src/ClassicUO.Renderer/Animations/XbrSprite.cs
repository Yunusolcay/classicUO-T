// SPDX-License-Identifier: BSD-2-Clause
//
// 2x xBR (Hyllian's xBR level 2, MIT license) on the CPU, for single sprites with transparency.
// A per-pixel port of shaders/xBR.fx: every output pixel takes the color of the source pixel or
// one of its four direct neighbours, chosen by weighted luminance distances, so colors are never
// blended (partial hue gray detection keeps working) yet near-equal colors still count as an
// edge, unlike MMPX which needs exactly equal colors and so rarely acts on UO's pre-rendered,
// many-colored animation frames.

using System;

namespace ClassicUO.Renderer.Animations
{
    public static class XbrSprite
    {
        // transparent pixels must never look like dark opaque outline pixels to the edge rules
        private const float TRANSPARENT_LUMA = -1000f;

        public static uint[] Magnify(ReadOnlySpan<uint> pixels, int width, int height)
        {
            if (width <= 0 || height <= 0 || pixels.Length < width * height)
            {
                throw new ArgumentException("Invalid image size");
            }

            float[] luma = new float[width * height];

            for (int i = 0; i < luma.Length; i++)
            {
                luma[i] = Luma(pixels[i]);
            }

            uint[] dst = new uint[width * height * 4];
            int dstWidth = width * 2;
            Span<float> b = stackalloc float[4], c = stackalloc float[4], d = stackalloc float[4], f = stackalloc float[4];
            Span<float> g = stackalloc float[4], h = stackalloc float[4], i4 = stackalloc float[4], i5 = stackalloc float[4];
            Span<float> h5 = stackalloc float[4], f4 = stackalloc float[4], ii = stackalloc float[4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float L(int dx, int dy) => luma[Index(width, height, x + dx, y + dy)];

                    float A = L(-1, -1), B = L(0, -1), C = L(1, -1);
                    float D = L(-1, 0), E = L(0, 0), F = L(1, 0);
                    float G = L(-1, 1), H = L(0, 1), I = L(1, 1);

                    // vectors in the shader's component order
                    b[0] = B; b[1] = D; b[2] = H; b[3] = F;
                    c[0] = C; c[1] = A; c[2] = G; c[3] = I;
                    d[0] = b[1]; d[1] = b[2]; d[2] = b[3]; d[3] = b[0]; // b.yzwx
                    f[0] = b[3]; f[1] = b[0]; f[2] = b[1]; f[3] = b[2]; // b.wxyz
                    g[0] = c[2]; g[1] = c[3]; g[2] = c[0]; g[3] = c[1]; // c.zwxy
                    h[0] = b[2]; h[1] = b[3]; h[2] = b[0]; h[3] = b[1]; // b.zwxy
                    ii[0] = c[3]; ii[1] = c[0]; ii[2] = c[1]; ii[3] = c[2]; // c.wxyz

                    i4[0] = L(2, 1); i4[1] = L(1, -2); i4[2] = L(-2, -1); i4[3] = L(-1, 2);   // I4 C1 A0 G5
                    i5[0] = L(1, 2); i5[1] = L(2, -1); i5[2] = L(-1, -2); i5[3] = L(-2, 1);   // I5 C4 A1 G0
                    h5[0] = L(0, 2); h5[1] = L(2, 0); h5[2] = L(0, -2); h5[3] = L(-2, 0);     // H5 F4 B1 D0
                    f4[0] = h5[1]; f4[1] = h5[2]; f4[2] = h5[3]; f4[3] = h5[0];               // h5.yzwx

                    uint cE = pixels[Index(width, height, x, y)];
                    uint cB = pixels[Index(width, height, x, y - 1)];
                    uint cD = pixels[Index(width, height, x - 1, y)];
                    uint cF = pixels[Index(width, height, x + 1, y)];
                    uint cH = pixels[Index(width, height, x, y + 1)];

                    for (int sub = 0; sub < 4; sub++)
                    {
                        // output pixel center inside the source pixel: 0.25 or 0.75
                        float fpx = (sub & 1) == 0 ? 0.25f : 0.75f;
                        float fpy = (sub & 2) == 0 ? 0.25f : 0.75f;

                        uint res = cE;

                        for (int k = 0; k < 4; k++)
                        {
                            bool fx = Ao[k] * fpy + Bo[k] * fpx > Co[k];
                            bool fxLeft = Ax[k] * fpy + Bx[k] * fpx > Cx[k];
                            bool fxUp = Ay[k] * fpy + By[k] * fpx > Cy[k];

                            bool irLv1 = E != f[k] && E != h[k];
                            bool irLv2Left = E != g[k] && d[k] != g[k];
                            bool irLv2Up = E != c[k] && b[k] != c[k];

                            float w1 = Wd(E, c[k], g[k], ii[k], h5[k], f4[k], h[k], f[k]);
                            float w2 = Wd(h[k], d[k], i5[k], f[k], i4[k], b[k], E, ii[k]);
                            float dfFg = Math.Abs(f[k] - g[k]);
                            float dfHc = Math.Abs(h[k] - c[k]);

                            bool edr = w1 < w2 && irLv1;
                            bool edrLeft = 2f * dfFg <= dfHc && irLv2Left;
                            bool edrUp = 2f * dfHc <= dfFg && irLv2Up;

                            if (edr && (fx || edrLeft && fxLeft || edrUp && fxUp))
                            {
                                bool px = Math.Abs(E - f[k]) <= Math.Abs(E - h[k]);

                                res = k switch
                                {
                                    0 => px ? cF : cH,
                                    1 => px ? cB : cF,
                                    2 => px ? cD : cB,
                                    _ => px ? cH : cD
                                };

                                break; // the shader's nc.x ? .. : nc.y ? .. priority
                            }
                        }

                        dst[(y * 2 + (sub >> 1)) * dstWidth + x * 2 + (sub & 1)] = res;
                    }
                }
            }

            return dst;
        }

        private static readonly float[] Ao = { 1f, -1f, -1f, 1f };
        private static readonly float[] Bo = { 1f, 1f, -1f, -1f };
        private static readonly float[] Co = { 1.5f, 0.5f, -0.5f, 0.5f };
        private static readonly float[] Ax = { 1f, -1f, -1f, 1f };
        private static readonly float[] Bx = { 0.5f, 2f, -0.5f, -2f };
        private static readonly float[] Cx = { 1f, 1f, -0.5f, 0f };
        private static readonly float[] Ay = { 1f, -1f, -1f, 1f };
        private static readonly float[] By = { 2f, 0.5f, -2f, -0.5f };
        private static readonly float[] Cy = { 2f, 0f, -1f, 0.5f };

        private static float Wd(float a, float b, float c, float d, float e, float f, float g, float h)
        {
            return Math.Abs(a - b) + Math.Abs(a - c) + Math.Abs(d - e) + Math.Abs(d - f) + 4f * Math.Abs(g - h);
        }

        // shader: dot(rgb, (14.352, 28.176, 5.472)) with rgb in 0..1; ABGR8 input
        private static float Luma(uint c)
        {
            if ((c >> 24) == 0)
            {
                return TRANSPARENT_LUMA;
            }

            return (c & 0xFF) / 255f * 14.352f + ((c >> 8) & 0xFF) / 255f * 28.176f + ((c >> 16) & 0xFF) / 255f * 5.472f;
        }

        private static int Index(int width, int height, int x, int y)
        {
            return Math.Clamp(y, 0, height - 1) * width + Math.Clamp(x, 0, width - 1);
        }
    }
}
