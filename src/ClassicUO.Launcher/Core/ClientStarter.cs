// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClassicUO.Launcher.Core
{
    internal static class ClientStarter
    {
        public const string BOOTSTRAP_EXE = "ClassicUO.exe";

        // Every profile gets its own settings file. ClassicUO keeps writing window size, fps,
        // last server etc. into it, so only the fields the launcher owns are replaced.
        public static string GetSettingsPath(string clientDirectory, ServerProfile profile)
        {
            return Path.Combine(clientDirectory, "Data", "Launcher", $"settings_{profile.Id}.json");
        }

        public static string WriteSettings(string clientDirectory, ServerProfile profile)
        {
            string path = GetSettingsPath(clientDirectory, profile);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            JsonObject json = MergeSettings(File.Exists(path) ? File.ReadAllText(path) : null, profile);

            AtomicFile.WriteAllText(path, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            return path;
        }

        internal static JsonObject MergeSettings(string? existingJson, ServerProfile profile)
        {
            JsonObject json;

            try
            {
                json = existingJson != null ? JsonNode.Parse(existingJson) as JsonObject ?? new JsonObject() : new JsonObject();
            }
            catch (JsonException)
            {
                json = new JsonObject();
            }

            if (profile.Username.Length != 0)
            {
                json["username"] = profile.Username;
            }

            // Account fields are only owned by the launcher when the player saved the password
            // here; otherwise whatever the player saved on ClassicUO's own login screen is kept.
            if (profile.SavePassword)
            {
                string password = PasswordProtector.Unprotect(profile.ProtectedPassword);

                // ClassicUO's own reversible format (same as its "save account" checkbox)
                json["password"] = password.Length != 0 ? ClassicUOCrypter.Encrypt(password) : string.Empty;
                json["saveaccount"] = true;
                json["autologin"] = profile.AutoLogin && password.Length != 0;
            }
            else if (profile.ClearClientPassword)
            {
                // the player turned "save password" off: remove what the launcher stored before
                json["password"] = string.Empty;
                json["saveaccount"] = false;
                json["autologin"] = false;
                profile.ClearClientPassword = false;
            }

            json["ip"] = profile.Host.Trim();
            json["port"] = profile.Port;
            json["ultimaonlinedirectory"] = profile.UltimaOnlineDirectory;
            json["clientversion"] = profile.ClientVersion.Trim();
            json["encryption"] = profile.UseEncryption ? 1 : 0;

            var plugins = new JsonArray();

            foreach (string plugin in profile.Plugins)
            {
                if (!string.IsNullOrWhiteSpace(plugin))
                {
                    plugins.Add(JsonValue.Create(plugin.Trim()));
                }
            }

            json["plugins"] = plugins;

            return json;
        }

        public static Process Start(string clientDirectory, ServerProfile profile)
        {
            string settingsPath = WriteSettings(clientDirectory, profile);
            ProcessStartInfo psi = CreateStartInfo(clientDirectory, settingsPath, profile.ExtraArguments);

            LauncherLog.Write($"Starting {psi.FileName} {string.Join(' ', psi.ArgumentList)}");

            return Process.Start(psi) ?? throw new InvalidOperationException("ClassicUO process could not be started");
        }

        internal static ProcessStartInfo CreateStartInfo(string clientDirectory, string settingsPath, string extraArguments)
        {
            string bootstrap = Path.Combine(clientDirectory, BOOTSTRAP_EXE);

            if (!File.Exists(bootstrap))
            {
                throw new FileNotFoundException(bootstrap);
            }

            ProcessStartInfo psi;

            if (OperatingSystem.IsWindows())
            {
                psi = new ProcessStartInfo(bootstrap);
            }
            else
            {
                // the bootstrap is a .NET Framework app: Linux/macOS run it with mono
                psi = new ProcessStartInfo("mono");
                psi.ArgumentList.Add(bootstrap);
            }

            // ClassicUO resolves relative paths (Data, Profiles, plugins) from the working directory
            psi.WorkingDirectory = clientDirectory;
            psi.UseShellExecute = false;
            psi.ArgumentList.Add("-settings");
            psi.ArgumentList.Add(settingsPath);

            foreach (string arg in SplitArguments(extraArguments))
            {
                psi.ArgumentList.Add(arg);
            }

            return psi;
        }

        // Splits on whitespace, keeping "quoted values" together.
        internal static List<string> SplitArguments(string? text)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var current = new StringBuilder();
            bool quoted = false;
            bool hasToken = false;

            foreach (char c in text)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    hasToken = true;
                }
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (hasToken)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        hasToken = false;
                    }
                }
                else
                {
                    current.Append(c);
                    hasToken = true;
                }
            }

            if (hasToken)
            {
                result.Add(current.ToString());
            }

            return result;
        }
    }
}
