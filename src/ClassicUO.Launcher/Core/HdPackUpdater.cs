// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicUO.Launcher.Core
{
    // Keeps <ClassicUO>/Data/HD/world in sync with the HD world pack published as a release of a
    // private GitHub repository (tools/hdpack/publish_pack.py). The pack is made from EA's art, so
    // it is never in the public client release; whoever may use it gets a read-only access token.
    // The release carries manifest.xml (ManifestCreator format, paths under Data/HD/world) and the
    // zip it names; ClientUpdater then downloads and installs only the changed images.
    internal static class HdPackUpdater
    {
        public const string MANIFEST_ASSET = "manifest.xml";
        public static readonly string PackPrefix = Path.Combine("Data", "HD", "world") + Path.DirectorySeparatorChar;

        public static bool IsConfigured(string repository, string accessToken) =>
            IsValidRepository(repository) && !string.IsNullOrWhiteSpace(accessToken);

        public static async Task<UpdateCheck> CheckAsync(ClientUpdater updater, string repository, string tag, string accessToken, CancellationToken token)
        {
            if (!IsValidRepository(repository))
            {
                throw new ArgumentException($"Invalid HD pack repository: {repository}");
            }

            accessToken = accessToken.Trim();
            tag = string.IsNullOrWhiteSpace(tag) ? LauncherConfig.DEFAULT_HD_PACK_TAG : tag.Trim();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var releaseUrl = new Uri($"https://api.github.com/repos/{repository.Trim()}/releases/tags/{Uri.EscapeDataString(tag)}");
            using var request = new HttpRequestMessage(HttpMethod.Get, releaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using HttpResponseMessage response = await ClientUpdater.Http.SendAsync(request, cts.Token);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.NotFound)
            {
                // GitHub answers 404 for a private repository the token cannot see
                throw new InvalidDataException($"HD pack not accessible ({(int)response.StatusCode}): check the access key");
            }

            response.EnsureSuccessStatusCode();

            using JsonDocument release = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
            Uri manifestUrl = FindAsset(release.RootElement, MANIFEST_ASSET) ?? throw new InvalidDataException("HD pack release has no manifest.xml");

            string xml = await ClientUpdater.DownloadManifestAsync(manifestUrl, accessToken, cts.Token);
            ClientManifest manifest = ClientUpdater.ParseManifest(xml);

            // the pack may only write its own folder, never the client itself
            foreach (ManifestFile file in manifest.Files)
            {
                if (!file.RelativePath.StartsWith(PackPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"HD pack file outside {PackPrefix}: {file.RelativePath}");
                }
            }

            Uri zipUrl = FindAsset(release.RootElement, manifest.Name) ?? throw new InvalidDataException($"HD pack release has no {manifest.Name}");

            return await updater.CheckAsync(manifest, zipUrl, accessToken, token);
        }

        // API URL of a release asset: downloading it with the token works for private repositories
        internal static Uri? FindAsset(JsonElement release, string name)
        {
            if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out JsonElement n) && n.GetString() == name
                    && asset.TryGetProperty("url", out JsonElement url) && Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? uri)
                    && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "api.github.com")
                {
                    return uri;
                }
            }

            return null;
        }

        // "owner/name" with GitHub's allowed characters
        internal static bool IsValidRepository(string? repository)
        {
            if (string.IsNullOrWhiteSpace(repository))
            {
                return false;
            }

            string[] parts = repository.Trim().Split('/');

            if (parts.Length != 2)
            {
                return false;
            }

            foreach (string part in parts)
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    return false;
                }

                foreach (char c in part)
                {
                    if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
