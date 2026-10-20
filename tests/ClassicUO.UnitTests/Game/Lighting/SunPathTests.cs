// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Game.Lighting;
using Microsoft.Xna.Framework;
using Xunit;

namespace ClassicUO.UnitTests.Game.Lighting
{
    public class SunPathTests
    {
        [Fact]
        public void Noon_GivesTheOriginalUoShadow()
        {
            float fade = SunPath.Get(12 * 60, out Vector2 offset);

            Assert.Equal(1f, fade, 4);
            Assert.Equal(SunPath.Classic.X, offset.X, 3);
            Assert.Equal(SunPath.Classic.Y, offset.Y, 3);
        }

        [Fact]
        public void Morning_FallsUpLeft_EveningDownRight()
        {
            SunPath.Get(8 * 60, out Vector2 morning);
            SunPath.Get(16 * 60, out Vector2 evening);

            Assert.True(morning.X < 0f && morning.Y < 0f);
            Assert.True(evening.X > 0f && evening.Y > 0f);
        }

        [Fact]
        public void LowSun_CastsLongerShadows_UpToTheLimit()
        {
            SunPath.Get(12 * 60, out Vector2 noon);
            SunPath.Get(9 * 60, out Vector2 morning);
            SunPath.Get(6 * 60 + 5, out Vector2 dawn);

            Assert.True(morning.Length() > noon.Length());
            Assert.True(dawn.Length() <= SunPath.MAX_LENGTH * noon.Length() + 0.001f);
        }

        [Fact]
        public void Horizon_FadesTheShadow()
        {
            Assert.Equal(0f, SunPath.Get(6 * 60, out _), 3);
            Assert.InRange(SunPath.Get(6 * 60 + 15, out _), 0f, 0.99f);
            Assert.Equal(1f, SunPath.Get(10 * 60, out _), 4);
        }

        [Fact]
        public void Direction_TurnsSmoothlyThroughTheDay()
        {
            SunPath.Get(7 * 60, out Vector2 previous);

            for (int m = 7 * 60 + 1; m <= 17 * 60; m++)
            {
                SunPath.Get(m, out Vector2 offset);
                float turn = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(previous), Vector2.Normalize(offset)), -1f, 1f));

                Assert.True(turn < 0.02f, $"jump at minute {m}");
                previous = offset;
            }
        }

        [Fact]
        public void Clock_FollowsTheServerTime()
        {
            SunClock.Sync(14, 30, 0);

            try
            {
                Assert.True(SunClock.ServerSynced);
                Assert.InRange(SunClock.GetMinutes(), 14 * 60 + 30, 14 * 60 + 31);
            }
            finally
            {
                SunClock.Reset();
            }
        }

        [Fact]
        public void Clock_IgnoresInvalidTimes()
        {
            SunClock.Sync(25, 0, 0);

            Assert.False(SunClock.ServerSynced);
        }

        [Fact]
        public void Clock_WithoutServer_RunsAtOsiPace()
        {
            DateTime start = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
            float a = SunClock.GetMinutesFromUtc(start);
            float b = SunClock.GetMinutesFromUtc(start.AddSeconds(50));

            Assert.Equal(10f, (b - a + SunClock.MINUTES_PER_DAY) % SunClock.MINUTES_PER_DAY, 2);
            Assert.Equal(a, SunClock.GetMinutesFromUtc(start.AddHours(2)), 2);
        }
    }
}
