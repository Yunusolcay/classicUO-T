// SPDX-License-Identifier: BSD-2-Clause

using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Lighting
{
    // Ambient light of the enhanced light map (docs/LIGHTING_PLAN.md, L7). Server light level
    // changes fade in over the transition time instead of jumping, and the night can take a cool
    // moonlight tint. Without tint the settled value is the original IsometricLevel gray.
    internal sealed class AmbientLight
    {
        // moonlight: blue up, red down, luma close to 1 so the tint does not change the brightness
        private static readonly Vector3 NIGHT_TINT = new Vector3(0.86f, 1f, 1.24f);
        // share of the tint at full darkness
        private const float NIGHT_TINT_STRENGTH = 0.6f;

        private bool _started;
        private float _from, _to, _current;
        private uint _startTicks;

        public float Level => _current;

        // target: this frame's ambient level (IsometricLevel, dark nights already applied).
        // Returns the ambient RGB, each channel in 0..1.
        public Vector3 Update(float target, uint ticks, float transitionSeconds, bool tint)
        {
            target = MathHelper.Clamp(target, 0f, 1f);

            if (!_started || transitionSeconds <= 0f)
            {
                _started = true;
                _from = _to = _current = target;
                _startTicks = ticks;
            }
            else if (target != _to)
            {
                // a new level while fading starts from where the fade is now
                _from = _current;
                _to = target;
                _startTicks = ticks;
            }

            if (_current != _to)
            {
                float t = (ticks - _startTicks) / (transitionSeconds * 1000f);
                _current = t >= 1f ? _to : MathHelper.Lerp(_from, _to, SmoothStep(t));
            }

            return GetColor(_current, tint);
        }

        // Next update jumps straight to the level: after the enhanced path was off, a fade from a
        // stale level would be wrong.
        public void Reset()
        {
            _started = false;
        }

        public static Vector3 GetColor(float level, bool tint)
        {
            Vector3 color = new Vector3(level);

            if (tint)
            {
                float night = (1f - level) * NIGHT_TINT_STRENGTH;
                color *= Vector3.Lerp(Vector3.One, NIGHT_TINT, night);
                color = Vector3.Clamp(color, Vector3.Zero, Vector3.One);
            }

            return color;
        }

        private static float SmoothStep(float t)
        {
            t = Math.Clamp(t, 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }
}
