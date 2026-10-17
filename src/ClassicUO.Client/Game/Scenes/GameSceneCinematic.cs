// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Scenes
{
    // Cinematic look hooks of the game scene (docs/CINEMATIC_PLAN.md)
    internal partial class GameScene
    {
        // the player's body is drawn above the tile position: center the vignette on the chest
        private const int CINEMATIC_FOCUS_RAISE = 30;

        private readonly CinematicRenderer _cinematicRenderer = new CinematicRenderer();

        private bool UseCinematic(GraphicsDevice device, RenderTarget2D worldTarget)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (!profile.CinematicLook || worldTarget == null)
            {
                return false;
            }

            if (!_cinematicRenderer.EnsureResources(device))
            {
                profile.CinematicLook = false;

                return false;
            }

            return true;
        }

        private void ApplyCinematic(UltimaBatcher2D batcher, RenderTarget2D source, RenderTarget2D destination, Matrix worldMatrix)
        {
            float focusY = 0.5f;

            if (_world.Player != null)
            {
                Vector2 player = _world.Player.GetScreenPosition() + new Vector2(22, 22 - CINEMATIC_FOCUS_RAISE);
                focusY = Vector2.Transform(player, worldMatrix).Y / destination.Height;
            }

            _cinematicRenderer.Apply(batcher, source, destination, focusY);
        }
    }
}
