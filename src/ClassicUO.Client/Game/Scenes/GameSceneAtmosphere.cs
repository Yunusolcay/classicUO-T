// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes
{
    // Region atmosphere hook of the game scene (docs/ATMOSPHERE_PLAN.md)
    internal partial class GameScene
    {
        private readonly AtmosphereRenderer _atmosphere = new AtmosphereRenderer();

        // over the drawn world, under the weather; before the lights, so the night darkens it too
        private void DrawAtmosphere(UltimaBatcher2D batcher, float layerDepth)
        {
            if (!ProfileManager.CurrentProfile.RegionAtmosphere)
            {
                _atmosphere.Reset();

                return;
            }

            Point origin = Camera.ScreenToWorld(Point.Zero);

            _atmosphere.Draw(
                batcher,
                _world,
                new Vector2(origin.X, origin.Y),
                Camera.Zoom,
                Camera.Bounds.Width,
                Camera.Bounds.Height,
                layerDepth,
                Time.Delta
            );
        }
    }
}
