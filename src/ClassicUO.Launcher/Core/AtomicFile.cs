// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;

namespace ClassicUO.Launcher.Core
{
    internal static class AtomicFile
    {
        // Write to a temp file first so a crash or full disk never leaves a half written config.
        public static void WriteAllText(string path, string content)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, content);
            File.Move(temp, path, true);
        }
    }

    internal static class LauncherLog
    {
        private static readonly object _lock = new();

        public static string FilePath => Path.Combine(AppContext.BaseDirectory, "launcher_log.txt");

        public static void Write(string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // logging must never break the launcher (e.g. read-only install folder)
            }
        }
    }
}
