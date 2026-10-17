// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassicUO.Launcher.Core
{
    internal sealed class ServerProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "SphereNet";
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 2593;
        public string UltimaOnlineDirectory { get; set; } = string.Empty;
        // empty: ClassicUO reads the version from client.exe in the UO directory
        public string ClientVersion { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool SavePassword { get; set; }
        // protected with PasswordProtector, never plain text
        public string ProtectedPassword { get; set; } = string.Empty;
        // set when "save password" is turned off, so the next launch also wipes it from ClassicUO
        public bool ClearClientPassword { get; set; }
        public bool AutoLogin { get; set; }
        public bool UseEncryption { get; set; }
        public List<string> Plugins { get; set; } = new();
        public string ExtraArguments { get; set; } = string.Empty;

        public ServerProfile Clone()
        {
            var copy = (ServerProfile)MemberwiseClone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Plugins = new List<string>(Plugins);

            return copy;
        }
    }

    internal sealed class LauncherConfig
    {
        public const string FILE_NAME = "launcher.json";
        // Shipped by the server owner next to the launcher: used as the first-run configuration
        // so players get the server address and update URL without typing anything.
        public const string DEFAULTS_FILE_NAME = "launcher.defaults.json";
        public const string DEFAULT_HD_PACK_TAG = "hd-world";

        public List<ServerProfile> Profiles { get; set; } = new();
        public string SelectedProfileId { get; set; } = string.Empty;
        // relative paths are relative to the launcher directory
        public string ClientDirectory { get; set; } = "ClassicUO";
        // manifest.xml produced by tools/ManifestCreator; empty disables updates
        public string UpdateManifestUrl { get; set; } = string.Empty;
        // where the release zip named in the manifest lives; empty = same folder as the manifest
        public string UpdateDownloadBaseUrl { get; set; } = string.Empty;
        public bool CheckUpdatesOnStart { get; set; } = true;
        public bool CloseAfterLaunch { get; set; }
        public string InstalledVersion { get; set; } = string.Empty;
        // HD world pack (HdPackUpdater): "owner/name" of the private repository with the pack
        // release, its tag, and the read-only access key protected like passwords
        public string HdPackRepository { get; set; } = string.Empty;
        public string HdPackTag { get; set; } = DEFAULT_HD_PACK_TAG;
        public string ProtectedHdPackToken { get; set; } = string.Empty;

        [JsonIgnore]
        public string FilePath { get; private set; } = string.Empty;

        public static string LauncherDirectory => AppContext.BaseDirectory;

        public string GetClientDirectory()
        {
            return Path.GetFullPath(Path.IsPathRooted(ClientDirectory) ? ClientDirectory : Path.Combine(LauncherDirectory, ClientDirectory));
        }

        public ServerProfile? GetSelectedProfile()
        {
            return Profiles.Find(p => p.Id == SelectedProfileId) ?? (Profiles.Count != 0 ? Profiles[0] : null);
        }

        public static LauncherConfig Load()
        {
            string portable = Path.Combine(LauncherDirectory, FILE_NAME);
            string roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "classicUO-T", FILE_NAME);

            LauncherConfig? config = TryRead(portable) ?? TryRead(roaming) ?? TryRead(Path.Combine(LauncherDirectory, DEFAULTS_FILE_NAME));

            if (config == null)
            {
                config = new LauncherConfig();
            }

            if (config.Profiles.Count == 0)
            {
                config.Profiles.Add(new ServerProfile());
            }

            // a launcher.json older than the HD pack option takes the repository from the defaults
            if (string.IsNullOrWhiteSpace(config.HdPackRepository))
            {
                config.HdPackRepository = TryRead(Path.Combine(LauncherDirectory, DEFAULTS_FILE_NAME))?.HdPackRepository ?? string.Empty;
            }

            config.FilePath = CanWriteTo(LauncherDirectory) ? portable : roaming;

            return config;
        }

        public void Save()
        {
            string? dir = Path.GetDirectoryName(FilePath);

            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(this, LauncherJsonContext.Default.LauncherConfig);
            AtomicFile.WriteAllText(FilePath, json);
        }

        internal static LauncherConfig? Parse(string json)
        {
            return JsonSerializer.Deserialize(json, LauncherJsonContext.Default.LauncherConfig);
        }

        private static LauncherConfig? TryRead(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                LauncherLog.Write($"Cannot read {path}: {ex.Message}");

                return null;
            }
        }

        private static bool CanWriteTo(string directory)
        {
            try
            {
                string probe = Path.Combine(directory, ".write_test");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(LauncherConfig))]
    internal sealed partial class LauncherJsonContext : JsonSerializerContext
    {
    }
}
