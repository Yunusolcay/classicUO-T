// SPDX-License-Identifier: BSD-2-Clause

using System;

namespace ClassicUO.Game.Lighting
{
    // Normal maps for the surface lighting (docs/SURFACE_LIGHTING_PLAN.md), made from the color image:
    // brightness stands in for height (painted stones, planks and leaves are lit on top and dark in
    // the gaps), its slope gives the normal and the depth below the surrounding average the cavity.
    // Output texel: r, g = normal x, y (128 = flat), b = normal z, a = 255 open .. darker in cavities.
    internal static class SurfaceNormalBuilder
    {
        // slope of the height (brightness 0..1 per pixel) to normal tilt
        private const float RELIEF = 3.0f;
        // radius of the neighborhood a cavity is measured against
        private const int CAVITY_RADIUS = 3;
        // depth below the neighborhood to cavity, and the darkest cavity
        private const float CAVITY_SCALE = 3.0f;
        private const float CAVITY_MAX = 0.6f;

        // pixels: premultiplied RGBA (r in the low byte), as uploaded to the atlas
        public static uint[] Build(uint[] pixels, int width, int height)
        {
            int count = width * height;
            var heights = new float[count];
            var solid = new bool[count];

            for (int i = 0; i < count; i++)
            {
                uint p = pixels[i];
                uint a = p >> 24;

                if (a == 0)
                {
                    continue;
                }

                // premultiplied: the color divided by alpha is the straight color
                float r = (p & 0xFF) / (float)a;
                float g = ((p >> 8) & 0xFF) / (float)a;
                float b = ((p >> 16) & 0xFF) / (float)a;

                heights[i] = Math.Min(1f, 0.2126f * r + 0.7152f * g + 0.0722f * b);
                solid[i] = true;
            }

            // a light blur keeps single noisy pixels from becoming spikes
            float[] smooth = BoxAverage(heights, solid, width, height, 1);
            float[] around = BoxAverage(heights, solid, width, height, CAVITY_RADIUS);
            var result = new uint[count];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;

                    if (!solid[i])
                    {
                        result[i] = Renderer.Lighting.SurfaceNormals.FLAT;

                        continue;
                    }

                    float center = smooth[i];
                    // the outline is not an edge of the surface: missing neighbors count as level
                    float left = x > 0 && solid[i - 1] ? smooth[i - 1] : center;
                    float right = x < width - 1 && solid[i + 1] ? smooth[i + 1] : center;
                    float up = y > 0 && solid[i - width] ? smooth[i - width] : center;
                    float down = y < height - 1 && solid[i + width] ? smooth[i + width] : center;

                    float nx = -(right - left) * 0.5f * RELIEF;
                    float ny = -(down - up) * 0.5f * RELIEF;
                    float length = MathF.Sqrt(nx * nx + ny * ny + 1f);

                    float cavity = Math.Clamp((around[i] - center) * CAVITY_SCALE, 0f, CAVITY_MAX);

                    result[i] = Pack(nx / length, ny / length, 1f / length, 1f - cavity);
                }
            }

            return result;
        }

        internal static uint Pack(float x, float y, float z, float open)
        {
            uint r = (uint)Math.Clamp((int)MathF.Round(x * 127f + 128f), 1, 255);
            uint g = (uint)Math.Clamp((int)MathF.Round(y * 127f + 128f), 1, 255);
            uint b = (uint)Math.Clamp((int)MathF.Round(z * 255f), 0, 255);
            uint a = (uint)Math.Clamp((int)MathF.Round(open * 255f), 0, 255);

            return (a << 24) | (b << 16) | (g << 8) | r;
        }

        // average of the solid pixels in a (2r+1)² box, by separable running sums
        private static float[] BoxAverage(float[] values, bool[] solid, int width, int height, int radius)
        {
            int count = width * height;
            var sumH = new float[count];
            var weightH = new float[count];

            for (int y = 0; y < height; y++)
            {
                float sum = 0f, weight = 0f;
                int row = y * width;

                for (int x = -radius; x < width + radius; x++)
                {
                    int enter = x + radius;
                    int leave = x - radius - 1;

                    if (enter < width && solid[row + enter])
                    {
                        sum += values[row + enter];
                        weight++;
                    }

                    if (leave >= 0 && solid[row + leave])
                    {
                        sum -= values[row + leave];
                        weight--;
                    }

                    if (x >= 0 && x < width)
                    {
                        sumH[row + x] = sum;
                        weightH[row + x] = weight;
                    }
                }
            }

            var result = new float[count];

            for (int x = 0; x < width; x++)
            {
                float sum = 0f, weight = 0f;

                for (int y = -radius; y < height + radius; y++)
                {
                    int enter = y + radius;
                    int leave = y - radius - 1;

                    if (enter < height)
                    {
                        sum += sumH[enter * width + x];
                        weight += weightH[enter * width + x];
                    }

                    if (leave >= 0)
                    {
                        sum -= sumH[leave * width + x];
                        weight -= weightH[leave * width + x];
                    }

                    if (y >= 0 && y < height)
                    {
                        int i = y * width + x;
                        result[i] = weight > 0.5f ? sum / weight : values[i];
                    }
                }
            }

            return result;
        }
    }
}
