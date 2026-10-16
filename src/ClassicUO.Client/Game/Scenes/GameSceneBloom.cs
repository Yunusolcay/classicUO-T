// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Scenes
{
    // Bloom hooks of the game scene (docs/BLOOM_PLAN.md)
    internal partial class GameScene
    {
        // glow threshold (luma) in full daylight and at full night: by day only the brightest
        // pixels glow, so sand and snow do not burn out
        private const float BLOOM_THRESHOLD_DAY = 0.85f;
        private const float BLOOM_THRESHOLD_NIGHT = 0.6f;
        private const float BLOOM_STRENGTH = 0.6f;

        private readonly BloomRenderer _bloomRenderer = new BloomRenderer();

        private bool UseBloom(GraphicsDevice device, RenderTarget2D worldTarget)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (!profile.Bloom || worldTarget == null)
            {
                return false;
            }

            if (!_bloomRenderer.EnsureResources(device))
            {
                profile.Bloom = false;

                return false;
            }

            return true;
        }

        private void PrepareBloom(UltimaBatcher2D batcher, Texture2D source)
        {
            // without enhanced lighting the night darkness is applied after this image: keep the day
            // threshold, or every bright ground pixel would glow under the darkness
            float day = _enhancedLightingActive ? MathHelper.Clamp(_world.Light.IsometricLevel, 0f, 1f) : 1f;
            float threshold = MathHelper.Lerp(BLOOM_THRESHOLD_NIGHT, BLOOM_THRESHOLD_DAY, day);

            _bloomRenderer.Prepare(batcher, source, threshold, BLOOM_STRENGTH);
        }
    }
}
