// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace ClassicUO.Launcher.Core
{
    internal static class UoPathDetector
    {
        // Files every client era since T2A has, either as .mul or as packed .uop
        private static readonly string[][] _requiredFiles =
        {
            new[] { "tiledata.mul" },
            new[] { "hues.mul" },
            new[] { "art.mul", "artLegacyMUL.uop" },
            new[] { "map0.mul", "map0LegacyMUL.uop" }
        };

        public static bool IsValidUoDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return false;
            }

            foreach (string[] alternatives in _requiredFiles)
            {
                if (!Array.Exists(alternatives, f => File.Exists(Path.Combine(path, f))))
                {
                    return false;
                }
            }

            return true;
        }

        public static string? Detect()
        {
            foreach (string candidate in GetCandidates())
            {
                if (IsValidUoDirectory(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        // Version of client.exe in the UO folder (what ClassicUO uses when no version is set)
        public static string? ReadClientVersion(string uoDirectory)
        {
            string exe = Path.Combine(uoDirectory, "client.exe");

            if (!File.Exists(exe))
            {
                return null;
            }

            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);

            return info.FileMajorPart == 0 && info.FileMinorPart == 0
                ? null
                : $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
        }

        private static IEnumerable<string> GetCandidates()
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (string path in ReadRegistry())
                {
                    yield return path;
                }

                string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                foreach (string root in new[] { pf86, pf })
                {
                    if (string.IsNullOrEmpty(root))
                    {
                        continue;
                    }

                    yield return Path.Combine(root, "Electronic Arts", "Ultima Online Classic");
                    yield return Path.Combine(root, "EA Games", "Ultima Online Classic");
                    yield return Path.Combine(root, "Ultima Online Classic");
                    yield return Path.Combine(root, "Ultima Online");
                }

                yield return @"C:\Ultima Online Classic";
                yield return @"C:\Ultima Online";
                yield return @"C:\UO";
            }

            // portable layout: UO files shipped next to the launcher
            yield return Path.Combine(LauncherConfig.LauncherDirectory, "UO");
            yield return Path.Combine(LauncherConfig.LauncherDirectory, "Ultima Online");
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static IEnumerable<string> ReadRegistry()
        {
            string[] keys =
            {
                @"SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic",
                @"SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic",
                @"SOFTWARE\Origin Worlds Online\Ultima Online\1.0",
                @"SOFTWARE\WOW6432Node\Origin Worlds Online\Ultima Online\1.0",
                @"SOFTWARE\Origin Worlds Online\Ultima Online Third Dawn\1.0",
                @"SOFTWARE\WOW6432Node\Origin Worlds Online\Ultima Online Third Dawn\1.0"
            };

            var found = new List<string>();

            foreach (string key in keys)
            {
                try
                {
                    using RegistryKey? k = Registry.LocalMachine.OpenSubKey(key);

                    if (k == null)
                    {
                        continue;
                    }

                    if (k.GetValue("InstallDir") is string dir && dir.Length != 0)
                    {
                        found.Add(dir);
                    }

                    if (k.GetValue("ExePath") is string exe && exe.Length != 0)
                    {
                        string? exeDir = Path.GetDirectoryName(exe);

                        if (!string.IsNullOrEmpty(exeDir))
                        {
                            found.Add(exeDir);
                        }
                    }
                }
                catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
                {
                    // not readable for this user: just skip that key
                }
            }

            return found;
        }
    }
}
