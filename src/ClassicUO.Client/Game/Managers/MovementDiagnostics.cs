// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    // Writes walk requests, server acks and per-frame player movement state to a CSV file
    // so step cadence, round trip times and visual gaps can be graphed and compared between
    // option combinations. Only active while Profile.MovementDiagnostics is enabled.
    //
    // Columns: ms,event,seq,dir,run,a,b,c,d
    //   send      a=walkTime    b=lateMs       c=creditMs     d=unaccepted
    //   turn_skip a=walkTime    b=unaccepted
    //   ack       a=rttMs       b=unaccepted
    //   deny      a=x           b=y
    //   frame     a=frameMs     b=stepsQueued  c=offsetX      d=offsetY  (only while moving)
    internal static class MovementDiagnostics
    {
        private static readonly double[] _sendTime = new double[256];
        private static StreamWriter _writer;
        private static double _lastFrame;
        private static double _lastFlush;
        private static bool _wasMoving;

        public static bool Enabled => ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.MovementDiagnostics;

        private static double Now => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

        public static void OnWalkSent(byte seq, Direction dir, bool run, int walkTime, int lateMs, int creditMs, int unaccepted)
        {
            if (!Enabled)
            {
                return;
            }

            double now = Now;
            _sendTime[seq] = now;
            Write(now, "send", seq, (int)dir, run, walkTime, lateMs, creditMs, unaccepted);
        }

        public static void OnTurnSkipped(Direction dir, int walkTime, int unaccepted)
        {
            if (Enabled)
            {
                Write(Now, "turn_skip", 0, (int)dir, false, walkTime, unaccepted, 0, 0);
            }
        }

        public static void OnAck(byte seq, int unaccepted)
        {
            if (!Enabled)
            {
                return;
            }

            double now = Now;
            double sent = _sendTime[seq];
            Write(now, "ack", seq, 0, false, sent > 0 ? (int)Math.Round(now - sent) : -1, unaccepted, 0, 0);
        }

        public static void OnDeny(byte seq, int x, int y)
        {
            if (Enabled)
            {
                Write(Now, "deny", seq, 0, false, x, y, 0, 0);
            }
        }

        public static void OnFrame(PlayerMobile player)
        {
            if (!Enabled)
            {
                Close();

                return;
            }

            double now = Now;
            double frameMs = _lastFrame > 0 ? now - _lastFrame : 0;
            _lastFrame = now;

            if (player == null)
            {
                return;
            }

            bool moving = player.Steps.Count != 0 || player.Walker.StepsCount != 0;

            // log one extra frame after the movement stops so the final gap is visible
            if (moving || _wasMoving)
            {
                Write
                (
                    now,
                    "frame",
                    0,
                    (int)player.Direction,
                    player.IsRunning,
                    (int)Math.Round(frameMs * 100),
                    player.Steps.Count,
                    (int)player.Offset.X,
                    (int)player.Offset.Y
                );
            }

            _wasMoving = moving;

            if (_writer != null && now - _lastFlush > 1000)
            {
                _writer.Flush();
                _lastFlush = now;
            }
        }

        public static void Close()
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.Dispose();
            }
            catch
            {
            }

            _writer = null;
        }

        private static void Write(double now, string evt, byte seq, int dir, bool run, int a, int b, int c, int d)
        {
            if (_writer == null && !Open())
            {
                return;
            }

            _writer.Write(now.ToString("F3", CultureInfo.InvariantCulture));
            _writer.Write(',');
            _writer.Write(evt);
            _writer.Write(',');
            _writer.Write(seq);
            _writer.Write(',');
            _writer.Write(dir);
            _writer.Write(',');
            _writer.Write(run ? 1 : 0);
            _writer.Write(',');
            _writer.Write(a);
            _writer.Write(',');
            _writer.Write(b);
            _writer.Write(',');
            _writer.Write(c);
            _writer.Write(',');
            _writer.WriteLine(d);
        }

        private static bool Open()
        {
            try
            {
                string dir = Path.Combine(CUOEnviroment.ExecutablePath, "Logs");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"movement_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                _writer = new StreamWriter(path, false) { AutoFlush = false };
                _writer.WriteLine("# frame rows: a = frame time in 1/100 ms");
                _writer.WriteLine("ms,event,seq,dir,run,a,b,c,d");
                _lastFlush = Now;
                Log.Info($"Movement diagnostics: {path}");

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Movement diagnostics disabled: {ex.Message}");
                ProfileManager.CurrentProfile.MovementDiagnostics = false;

                return false;
            }
        }
    }
}
