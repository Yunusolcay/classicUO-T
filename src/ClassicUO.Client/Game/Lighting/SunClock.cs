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

        public static void Reset()
        {
            _synced = false;
        }

        // UO minutes since midnight, 0..1440
        public static float GetMinutes()
        {
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
