// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.GameObjects
{
    // Hover/target outline (docs/OUTLINE_PLAN.md): the hovered mobile and the last target get a soft
    // outline in their notoriety color, drawn as one color copies of the mobile around it.
    internal static class MobileOutline
    {
        // x, y: offset in world pixels, z: opacity. A sharp inner ring and a faint outer one.
        public static readonly Vector3[] Offsets =
        {
            new Vector3(-1, 0, 0.9f), new Vector3(1, 0, 0.9f), new Vector3(0, -1, 0.9f), new Vector3(0, 1, 0.9f),
            new Vector3(-1, -1, 0.6f), new Vector3(1, -1, 0.6f), new Vector3(-1, 1, 0.6f), new Vector3(1, 1, 0.6f),
            new Vector3(-2, 0, 0.3f), new Vector3(2, 0, 0.3f), new Vector3(0, -2, 0.3f), new Vector3(0, 2, 0.3f)
        };

        public static bool ShouldOutline(Mobile mobile)
        {
            Profile profile = ProfileManager.CurrentProfile;

            if (profile == null || !profile.MobileOutline || mobile.IsDestroyed || mobile.IsHidden || mobile == mobile.World.Player)
            {
                return false;
            }

            return ReferenceEquals(SelectedObject.Object, mobile)
                || mobile.World.TargetManager.LastTargetInfo.Serial == mobile.Serial;
        }

        // the notoriety color as a solid text hue: every opaque pixel takes it
        public static Vector3 GetHue(Mobile mobile)
        {
            ushort hue = Notoriety.GetHue(mobile.NotorietyFlag);

            return new Vector3(hue > 0 ? hue - 1 : 0, ShaderHueTranslator.SHADER_TEXT_HUE, 1f);
        }
    }
}
