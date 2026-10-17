// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ClassicUO.Launcher.Core
{
    internal sealed record ManifestFile(string RelativePath, string Md5);

    internal sealed record ClientManifest(string Name, string Version, IReadOnlyList<ManifestFile> Files);

    // AccessToken: sent with the archive download (a private GitHub release asset), else null
    internal sealed record UpdateCheck(ClientManifest Manifest, IReadOnlyList<ManifestFile> Outdated, Uri ZipUrl, string? AccessToken = null)
    {
        public bool UpToDate => Outdated.Count == 0;
    }

    internal readonly record struct UpdateProgress(string Stage, double Fraction);

    // Updates the ClassicUO folder from a manifest.xml made by tools/ManifestCreator (the same
    // format the upstream Deploy workflow publishes) plus the release zip it names. Only files
    // whose MD5 differs are replaced; files not in the manifest (settings, profiles, plugins
    // the player added) are never touched.
    internal sealed class ClientUpdater
    {
        private const string WORK_DIR = ".update";
        private const long MAX_MANIFEST_BYTES = 5L * 1024 * 1024;
        private const long MAX_ZIP_BYTES = 2L * 1024 * 1024 * 1024;
        private const long MAX_ENTRY_BYTES = 512L * 1024 * 1024;

        internal static readonly HttpClient Http = CreateHttpClient();

        private readonly string _clientDirectory;

        public ClientUpdater(string clientDirectory)
        {
            _clientDirectory = Path.GetFullPath(clientDirectory);
        }

        public async Task<UpdateCheck> CheckAsync(string manifestUrl, string downloadBaseUrl, CancellationToken token)
        {
            Uri manifestUri = ValidateUrl(ExpandUrl(manifestUrl));
            Uri baseUri = string.IsNullOrWhiteSpace(downloadBaseUrl) ? new Uri(manifestUri, "./") : ValidateUrl(EnsureTrailingSlash(ExpandUrl(downloadBaseUrl)));

            string xml;

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                xml = await DownloadManifestAsync(manifestUri, null, cts.Token);
            }

            ClientManifest manifest = ParseManifest(xml);
            Uri zipUrl = new Uri(baseUri, Uri.EscapeDataString(manifest.Name));
            ValidateUrl(zipUrl.AbsoluteUri);

            List<ManifestFile> outdated = await Task.Run(() => FindOutdated(manifest), token);

            return new UpdateCheck(manifest, outdated, zipUrl);
        }

        // The same check for a manifest fetched elsewhere (HdPackUpdater), with the archive at zipUrl.
        public async Task<UpdateCheck> CheckAsync(ClientManifest manifest, Uri zipUrl, string? accessToken, CancellationToken token)
        {
            ValidateUrl(zipUrl.AbsoluteUri);
            List<ManifestFile> outdated = await Task.Run(() => FindOutdated(manifest), token);

            return new UpdateCheck(manifest, outdated, zipUrl, accessToken);
        }

        internal static async Task<string> DownloadManifestAsync(Uri url, string? accessToken, CancellationToken token)
        {
            using HttpRequestMessage request = CreateRequest(url, accessToken);
            using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > MAX_MANIFEST_BYTES)
            {
                throw new InvalidDataException("Manifest is too large");
            }

            return await response.Content.ReadAsStringAsync(token);
        }

        // A GitHub release asset behind a token is fetched from its API URL as a binary; the API
        // redirects to storage and HttpClient drops the Authorization header on that redirect.
        private static HttpRequestMessage CreateRequest(Uri url, string? accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.ParseAdd("application/octet-stream");
            }

            return request;
        }

        public async Task ApplyAsync(UpdateCheck check, IProgress<UpdateProgress> progress, CancellationToken token)
        {
            if (check.UpToDate)
            {
                return;
            }

            // refuse before downloading anything if ClassicUO is running from this folder
            EnsureNotLocked(check.Outdated);

            string work = Path.Combine(_clientDirectory, WORK_DIR);
            string zipPath = Path.Combine(work, "release.zip");
            string staging = Path.Combine(work, "staging");

            TryDeleteDirectory(work);
            Directory.CreateDirectory(staging);

            try
            {
                await DownloadAsync(check.ZipUrl, check.AccessToken, zipPath, progress, token);
                await Task.Run(() => ExtractVerified(zipPath, staging, check.Outdated, progress, token), token);

                EnsureNotLocked(check.Outdated);

                for (int i = 0; i < check.Outdated.Count; i++)
                {
                    ManifestFile file = check.Outdated[i];
                    string target = ResolveInside(_clientDirectory, file.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(ResolveInside(staging, file.RelativePath), target, true);
                    progress.Report(new UpdateProgress("install", (i + 1) / (double)check.Outdated.Count));
                }

                LauncherLog.Write($"Updated {check.Outdated.Count} file(s) to {check.Manifest.Version}");
            }
            finally
            {
                TryDeleteDirectory(work);
            }
        }

        internal static ClientManifest ParseManifest(string xml)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true
            };

            var files = new List<ManifestFile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? name = null, version = null;

            using var reader = XmlReader.Create(new StringReader(xml), settings);

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.Name == "release")
                {
                    // the first release is the current one
                    if (name != null)
                    {
                        break;
                    }

                    name = reader.GetAttribute("name");
                    version = reader.GetAttribute("version") ?? string.Empty;
                }
                else if (reader.Name == "file" && name != null)
                {
                    string path = NormalizeRelativePath(reader.GetAttribute("filename"));
                    string hash = (reader.GetAttribute("hash") ?? string.Empty).ToLowerInvariant();

                    if (hash.Length != 32 || !IsHex(hash))
                    {
                        throw new InvalidDataException($"Invalid hash for {path}");
                    }

                    if (seen.Add(path))
                    {
                        files.Add(new ManifestFile(path, hash));
                    }
                }
            }

            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0 || name.Contains(".."))
            {
                throw new InvalidDataException("Manifest has no valid release name");
            }

            if (files.Count == 0)
            {
                throw new InvalidDataException("Manifest lists no files");
            }

            return new ClientManifest(name, version!, files);
        }

        internal static string NormalizeRelativePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidDataException("Empty file name in manifest");
            }

            string[] parts = path.Split('/', '\\');

            foreach (string part in parts)
            {
                if (part.Length == 0 || part == "." || part == ".." || part.Contains(':') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidDataException($"Unsafe file name in manifest: {path}");
                }
            }

            return string.Join(Path.DirectorySeparatorChar, parts);
        }

        // Full path of relativePath under root; throws if it would escape root.
        internal static string ResolveInside(string root, string relativePath)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            // normalize first: on Linux/macOS "..\x" is otherwise a plain file name, not an escape
            string full = Path.GetFullPath(Path.Combine(fullRoot, NormalizeRelativePath(relativePath)));
            StringComparison cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            if (!full.StartsWith(fullRoot, cmp))
            {
                throw new InvalidDataException($"Path escapes the client folder: {relativePath}");
            }

            return full;
        }

        // ClassicUO release name of this platform, as used by the Deploy workflow
        public static string PlatformRid =>
            OperatingSystem.IsWindows() ? "win-x64" : OperatingSystem.IsMacOS() ? "osx-x64" : "linux-x64";

        // "{rid}" in a configured URL selects the per-platform manifest/archive
        public static string ExpandUrl(string url) => (url ?? string.Empty).Replace("{rid}", PlatformRid, StringComparison.OrdinalIgnoreCase);

        internal static Uri ValidateUrl(string url)
        {
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri? uri))
            {
                throw new ArgumentException($"Invalid URL: {url}");
            }

            // plain http only for local testing, everything players download goes over https
            if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            {
                throw new ArgumentException($"Only https URLs are allowed: {url}");
            }

            return uri;
        }

        internal List<ManifestFile> FindOutdated(ClientManifest manifest)
        {
            var outdated = new List<ManifestFile>();

            foreach (ManifestFile file in manifest.Files)
            {
                string path = ResolveInside(_clientDirectory, file.RelativePath);

                if (!File.Exists(path) || !string.Equals(ComputeMd5(path), file.Md5, StringComparison.Ordinal))
                {
                    outdated.Add(file);
                }
            }

            return outdated;
        }

        private static async Task DownloadAsync(Uri url, string? accessToken, string path, IProgress<UpdateProgress> progress, CancellationToken token)
        {
            using HttpRequestMessage request = CreateRequest(url, accessToken);
            using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;

            if (total > MAX_ZIP_BYTES)
            {
                throw new InvalidDataException("Release archive is too large");
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(token);
            await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long received = 0;
            int read;

            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                received += read;

                if (received > MAX_ZIP_BYTES)
                {
                    throw new InvalidDataException("Release archive is too large");
                }

                await target.WriteAsync(buffer.AsMemory(0, read), token);

                if (total > 0)
                {
                    progress.Report(new UpdateProgress("download", received / (double)total.Value));
                }
            }
        }

        internal static void ExtractVerified(string zipPath, string staging, IReadOnlyList<ManifestFile> files, IProgress<UpdateProgress>? progress, CancellationToken token)
        {
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (entry.Name.Length == 0)
                {
                    continue; // directory
                }

                try
                {
                    entries[NormalizeRelativePath(entry.FullName)] = entry;
                }
                catch (InvalidDataException)
                {
                    // unsafe names are ignored; if the manifest needs them the lookup below fails
                }
            }

            for (int i = 0; i < files.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                ManifestFile file = files[i];

                if (!entries.TryGetValue(file.RelativePath, out ZipArchiveEntry? entry))
                {
                    throw new InvalidDataException($"{file.RelativePath} is missing from the release archive");
                }

                if (entry.Length > MAX_ENTRY_BYTES)
                {
                    throw new InvalidDataException($"{file.RelativePath} is too large");
                }

                string target = ResolveInside(staging, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                using (Stream source = entry.Open())
                using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CopyLimited(source, output, MAX_ENTRY_BYTES);
                }

                if (!string.Equals(ComputeMd5(target), file.Md5, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"{file.RelativePath} does not match the manifest hash");
                }

                progress?.Report(new UpdateProgress("verify", (i + 1) / (double)files.Count));
            }
        }

        private void EnsureNotLocked(IReadOnlyList<ManifestFile> files)
        {
            foreach (ManifestFile file in files)
            {
                string path = ResolveInside(_clientDirectory, file.RelativePath);

                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    throw new ClientRunningException(file.RelativePath);
                }
            }
        }

        private static void CopyLimited(Stream source, Stream target, long limit)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            int read;

            // the declared entry size can lie: count what is actually inflated
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;

                if (total > limit)
                {
                    throw new InvalidDataException("Archive entry is larger than allowed");
                }

                target.Write(buffer, 0, read);
            }
        }

        internal static string ComputeMd5(string path)
        {
            using FileStream stream = File.OpenRead(path);

            return Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static string EnsureTrailingSlash(string url)
        {
            url = url.Trim();

            return url.EndsWith('/') ? url : url + "/";
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LauncherLog.Write($"Cannot clean {path}: {ex.Message}");
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                // never let a redirect downgrade an https download to plain http
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };

            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("classicUO-T-Launcher/1.0");

            return client;
        }
    }

    internal sealed class ClientRunningException : IOException
    {
        public ClientRunningException(string file) : base($"{file} is in use") => File = file;

        public string File { get; }
    }
}
