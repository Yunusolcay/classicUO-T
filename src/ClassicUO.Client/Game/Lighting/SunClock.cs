// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Diagnostics;

namespace ClassicUO.Game.Lighting
{
    // Time of day for the moving sun shadows (docs/SHADOWS_PLAN.md, S7). The server time packet
    // (0x5B) sets it when the shard sends one; between packets, and when no packet ever came, the
    // clock runs at the OSI pace: one UO minute every 5 real seconds, a UO day in 2 real hours.
    // Without a server time it is taken from the UTC clock, so every client shows the same hour.
    internal static class SunClock
    {
        public const double REAL_SECONDS_PER_UO_MINUTE = 5.0;
        public const int MINUTES_PER_DAY = 24 * 60;

        private static bool _synced;
        // test only ("sunspeed" / "sunstart" launch arguments): runs the clock faster from a start hour
        private static float _speed = 1f;
        private static double _testStartMinutes;
        private static long _testStartTimestamp;
        private static double _syncMinutes;
        private static long _syncTimestamp;

        // true once the server sent its time
        public static bool ServerSynced => _synced;

        public static void Sync(int hour, int minute, int second)
        {
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
            {
                return;
            }

            _syncMinutes = hour * 60 + minute + second / 60.0;
            _syncTimestamp = Stopwatch.GetTimestamp();
            _synced = true;
        }

        // speed > 1 runs the clock that many times faster from startHour, ignoring the server time.
        // The clock starts on its first query (the first drawn world frame), not at launch.
        public static void SetTestSpeed(float speed, float startHour)
        {
            _speed = speed > 1f ? speed : 1f;
            _testStartMinutes = Math.Clamp(startHour, 0f, 24f) * 60.0;
            _testStartTimestamp = 0;
        }

        public static void Reset()
        {
            _synced = false;
        }

        // UO minutes since midnight, 0..1440
        public static float GetMinutes()
        {
            if (_speed > 1f)
            {
                if (_testStartTimestamp == 0)
                {
                    _testStartTimestamp = Stopwatch.GetTimestamp();
                    Utility.Logging.Log.Info($"Sun test clock started: {DateTime.Now:HH:mm:ss.fff} at minute {_testStartMinutes:0}, speed {_speed}");
                }

                double elapsed = Stopwatch.GetElapsedTime(_testStartTimestamp).TotalSeconds;

                return Wrap(_testStartMinutes + elapsed * _speed / REAL_SECONDS_PER_UO_MINUTE);
            }

            if (_synced)
            {
                double elapsed = Stopwatch.GetElapsedTime(_syncTimestamp).TotalSeconds;

                return Wrap(_syncMinutes + elapsed / REAL_SECONDS_PER_UO_MINUTE);
            }

            return GetMinutesFromUtc(DateTime.UtcNow);
        }

        public static float GetMinutesFromUtc(DateTime utc)
        {
            double seconds = (utc - DateTime.UnixEpoch).TotalSeconds;

            return Wrap(seconds / REAL_SECONDS_PER_UO_MINUTE);
        }

        private static float Wrap(double minutes)
        {
            minutes %= MINUTES_PER_DAY;

            return (float) (minutes < 0 ? minutes + MINUTES_PER_DAY : minutes);
        }
    }
}
