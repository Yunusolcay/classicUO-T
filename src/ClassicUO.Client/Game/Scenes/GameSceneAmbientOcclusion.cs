// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Lighting;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Scenes
{
    // Ambient occlusion at the foot of walls and large objects (docs/SHADOWS_PLAN.md, S6). The
    // visible occluders are collected while the render lists are filled; the soft shadow pass
    // draws an ellipse on the ground under each one.
    internal partial class GameScene
    {
        private readonly List<Vector3> _occluders = new List<Vector3>();
        private bool _collectOccluders;

        // Called before the render lists are filled
        private void BeginOccluders()
        {
            _occluders.Clear();

            Profile profile = ProfileManager.CurrentProfile;
            _collectOccluders = profile.AmbientOcclusion && profile.ShadowsEnabled && profile.SoftShadows;
        }

        private void AddOccluder(GameObject obj, ref StaticTiles data)
        {
            if (!_collectOccluders || !ShadowSettings.IsOccluder(ref data) || obj.Z >= _maxZ)
            {
                return;
            }

            // only things standing on the ground: an upper floor's walls would darken the walls below
            if (obj.Z - _world.Map.GetTileZ(obj.X, obj.Y) > ShadowSettings.OCCLUSION_MAX_HEIGHT)
            {
                return;
            }

            // one tile closer than the object, half a priority step above it: the floor in front
            // darkens, a mobile standing there (priority + 1) does not
            float depth = obj.CalculateDepthZ() + 1.005f;

            _occluders.Add(new Vector3(obj.RealScreenPosition.X + 22, obj.RealScreenPosition.Y + 22, depth));
        }

        // Called after BeginDeferredShadows
        private void DrawOccluders(UltimaBatcher2D batcher)
        {
            foreach (Vector3 occluder in _occluders)
            {
                batcher.DrawAmbientOcclusion(occluder.X, occluder.Y, occluder.Z, ShadowSettings.OCCLUSION_STRENGTH);
            }

            _occluders.Clear();
        }
    }
}
