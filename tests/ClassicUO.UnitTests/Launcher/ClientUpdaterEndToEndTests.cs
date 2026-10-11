using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Launcher.Core;
using Xunit;

namespace ClassicUO.UnitTests.Launcher
{
    // Serves a manifest.xml + release zip from a loopback HTTP listener and runs the real
    // check/download/verify/install path against a fake ClassicUO folder.
    public sealed class ClientUpdaterEndToEndTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "cuo_launcher_e2e_" + Guid.NewGuid().ToString("N"));
        private readonly HttpListener _listener = new();
        private readonly string _baseUrl;
        private byte[] _manifest = Array.Empty<byte>();
        private byte[] _zip = Array.Empty<byte>();

        public ClientUpdaterEndToEndTests()
        {
            Directory.CreateDirectory(_root);

            int port = GetFreePort();
            _baseUrl = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(_baseUrl);
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public void Dispose()
        {
            _listener.Close();

            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public async Task Updates_Only_Changed_Files_And_Keeps_Player_Files()
        {
            string client = Path.Combine(_root, "ClassicUO");
            Directory.CreateDirectory(Path.Combine(client, "Data", "Profiles"));
            File.WriteAllText(Path.Combine(client, "ClassicUO.exe"), "v1");
            File.WriteAllText(Path.Combine(client, "cuo.dll"), "same");
            File.WriteAllText(Path.Combine(client, "settings.json"), "player settings");
            File.WriteAllText(Path.Combine(client, "Data", "Profiles", "p.json"), "player profile");

            Publish(("ClassicUO.exe", "v2"), ("cuo.dll", "same"), ("Data/Client/new.bin", "new"));

            var updater = new ClientUpdater(client);
            UpdateCheck check = await updater.CheckAsync(_baseUrl + "manifest.xml", string.Empty, CancellationToken.None);

            Assert.Equal(2, check.Outdated.Count); // ClassicUO.exe + new.bin, cuo.dll unchanged

            await updater.ApplyAsync(check, new Progress<UpdateProgress>(), CancellationToken.None);

            Assert.Equal("v2", File.ReadAllText(Path.Combine(client, "ClassicUO.exe")));
            Assert.Equal("new", File.ReadAllText(Path.Combine(client, "Data", "Client", "new.bin")));
            Assert.Equal("player settings", File.ReadAllText(Path.Combine(client, "settings.json")));
            Assert.Equal("player profile", File.ReadAllText(Path.Combine(client, "Data", "Profiles", "p.json")));
            Assert.False(Directory.Exists(Path.Combine(client, ".update")));

            UpdateCheck again = await updater.CheckAsync(_baseUrl + "manifest.xml", string.Empty, CancellationToken.None);
            Assert.True(again.UpToDate);
        }

        [Fact]
        public async Task Tampered_Archive_Changes_Nothing()
        {
            string client = Path.Combine(_root, "ClassicUO");
            Directory.CreateDirectory(client);
            File.WriteAllText(Path.Combine(client, "ClassicUO.exe"), "v1");

            Publish(("ClassicUO.exe", "v2"));
            _zip = Zip(("ClassicUO.exe", "malware")); // archive swapped after the manifest was made

            var updater = new ClientUpdater(client);
            UpdateCheck check = await updater.CheckAsync(_baseUrl + "manifest.xml", string.Empty, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidDataException>(() => updater.ApplyAsync(check, new Progress<UpdateProgress>(), CancellationToken.None));
            Assert.Equal("v1", File.ReadAllText(Path.Combine(client, "ClassicUO.exe")));
        }

        private void Publish(params (string name, string content)[] files)
        {
            var sb = new StringBuilder();
            sb.Append("""<?xml version="1.0" encoding="utf-8"?><releases><release name="ClassicUO-win-x64-release.zip" version="1.1.0.42"><files>""");

            foreach (var (name, content) in files)
            {
                // ManifestCreator writes Windows separators
                sb.Append($"""<file filename="{name.Replace('/', '\\')}" hash="{Md5(content)}" />""");
            }

            sb.Append("</files></release></releases>");

            _manifest = Encoding.UTF8.GetBytes(sb.ToString());
            _zip = Zip(files);
        }

        private static byte[] Zip(params (string name, string content)[] files)
        {
            using var ms = new MemoryStream();

            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var (name, content) in files)
                {
                    using var w = new StreamWriter(zip.CreateEntry(name).Open());
                    w.Write(content);
                }
            }

            return ms.ToArray();
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;

                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                string path = ctx.Request.Url!.AbsolutePath;
                byte[]? body = path.EndsWith("/manifest.xml") ? _manifest : path.EndsWith("/ClassicUO-win-x64-release.zip") ? _zip : null;

                ctx.Response.StatusCode = body == null ? 404 : 200;

                if (body != null)
                {
                    ctx.Response.ContentLength64 = body.Length;
                    await ctx.Response.OutputStream.WriteAsync(body);
                }

                ctx.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();

            return port;
        }

        private static string Md5(string content)
        {
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        }
    }
}
