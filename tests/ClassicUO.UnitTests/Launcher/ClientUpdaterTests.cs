using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ClassicUO.Launcher.Core;
using Xunit;

namespace ClassicUO.UnitTests.Launcher
{
    public sealed class ClientUpdaterTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "cuo_launcher_tests_" + Guid.NewGuid().ToString("N"));

        public ClientUpdaterTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void Parses_ManifestCreator_Output()
        {
            const string xml = """
                <?xml version="1.0" encoding="utf-8" standalone="yes"?>
                <releases>
                	<release name="ClassicUO-win-x64-release.zip" version="release">
                		<files>
                			<file filename="ClassicUO.exe" hash="0123456789ABCDEF0123456789abcdef" />
                			<file filename="Data\Client\Fonts\a.ttf" hash="ffffffffffffffffffffffffffffffff" />
                		</files>
                	</release>
                </releases>
                """;

            ClientManifest m = ClientUpdater.ParseManifest(xml);

            Assert.Equal("ClassicUO-win-x64-release.zip", m.Name);
            Assert.Equal(2, m.Files.Count);
            Assert.Equal("0123456789abcdef0123456789abcdef", m.Files[0].Md5);
            Assert.Equal(Path.Combine("Data", "Client", "Fonts", "a.ttf"), m.Files[1].RelativePath);
        }

        [Theory]
        [InlineData(@"..\evil.dll")]
        [InlineData("../evil.dll")]
        [InlineData(@"Data\..\..\evil.dll")]
        [InlineData(@"C:\Windows\evil.dll")]
        [InlineData("/etc/passwd")]
        [InlineData(@"\\server\share\evil.dll")]
        [InlineData("")]
        public void Rejects_Unsafe_Manifest_Paths(string filename)
        {
            string xml = $"""<releases><release name="a.zip" version="1"><files><file filename="{filename}" hash="0123456789abcdef0123456789abcdef" /></files></release></releases>""";

            Assert.Throws<InvalidDataException>(() => ClientUpdater.ParseManifest(xml));
        }

        [Theory]
        [InlineData("../a.zip")]
        [InlineData("sub/a.zip")]
        public void Rejects_Unsafe_Release_Name(string name)
        {
            string xml = $"""<releases><release name="{name}" version="1"><files><file filename="a" hash="0123456789abcdef0123456789abcdef" /></files></release></releases>""";

            Assert.Throws<InvalidDataException>(() => ClientUpdater.ParseManifest(xml));
        }

        [Fact]
        public void Rejects_Dtd_And_External_Entities()
        {
            const string xml = """
                <?xml version="1.0"?>
                <!DOCTYPE releases [ <!ENTITY x SYSTEM "file:///c:/windows/win.ini"> ]>
                <releases><release name="a.zip" version="&x;"><files><file filename="a" hash="0123456789abcdef0123456789abcdef" /></files></release></releases>
                """;

            Assert.ThrowsAny<System.Xml.XmlException>(() => ClientUpdater.ParseManifest(xml));
        }

        [Fact]
        public void Rejects_Bad_Hash()
        {
            const string xml = """<releases><release name="a.zip" version="1"><files><file filename="a" hash="nothex" /></files></release></releases>""";

            Assert.Throws<InvalidDataException>(() => ClientUpdater.ParseManifest(xml));
        }

        [Theory]
        [InlineData("https://example.com/manifest.xml", true)]
        [InlineData("http://127.0.0.1:8080/manifest.xml", true)]
        [InlineData("http://localhost/manifest.xml", true)]
        [InlineData("http://example.com/manifest.xml", false)]
        [InlineData("ftp://example.com/manifest.xml", false)]
        [InlineData("file:///c:/manifest.xml", false)]
        [InlineData("not a url", false)]
        public void Only_Https_Or_Local_Http(string url, bool ok)
        {
            if (ok)
            {
                ClientUpdater.ValidateUrl(url);
            }
            else
            {
                Assert.Throws<ArgumentException>(() => ClientUpdater.ValidateUrl(url));
            }
        }

        [Fact]
        public void Finds_Only_Changed_Or_Missing_Files()
        {
            File.WriteAllText(Path.Combine(_root, "same.txt"), "same");
            File.WriteAllText(Path.Combine(_root, "changed.txt"), "old");
            File.WriteAllText(Path.Combine(_root, "settings.json"), "{ }"); // not in manifest: untouched

            var manifest = new ClientManifest("a.zip", "1", new List<ManifestFile>
            {
                new("same.txt", Md5("same")),
                new("changed.txt", Md5("new")),
                new(Path.Combine("sub", "missing.txt"), Md5("x"))
            });

            List<ManifestFile> outdated = new ClientUpdater(_root).FindOutdated(manifest);

            Assert.Equal(new[] { "changed.txt", Path.Combine("sub", "missing.txt") }, outdated.ConvertAll(f => f.RelativePath));
        }

        [Fact]
        public void Extracts_And_Verifies_Files()
        {
            string zip = CreateZip(("ClassicUO.exe", "exe"), ("Data/Client/x.bin", "bin"));
            string staging = Path.Combine(_root, "staging");
            var files = new List<ManifestFile> { new("ClassicUO.exe", Md5("exe")), new(Path.Combine("Data", "Client", "x.bin"), Md5("bin")) };

            ClientUpdater.ExtractVerified(zip, staging, files, null, CancellationToken.None);

            Assert.Equal("bin", File.ReadAllText(Path.Combine(staging, "Data", "Client", "x.bin")));
        }

        [Fact]
        public void Refuses_File_With_Wrong_Hash()
        {
            string zip = CreateZip(("ClassicUO.exe", "tampered"));
            var files = new List<ManifestFile> { new("ClassicUO.exe", Md5("original")) };

            Assert.Throws<InvalidDataException>(() => ClientUpdater.ExtractVerified(zip, Path.Combine(_root, "staging"), files, null, CancellationToken.None));
        }

        [Fact]
        public void Zip_Slip_Entries_Are_Never_Written_Outside()
        {
            string zip = CreateZip(("../../outside.txt", "evil"));
            // even if a manifest asked for it, the name cannot be resolved inside the staging folder
            var files = new List<ManifestFile> { new("outside.txt", Md5("evil")) };

            Assert.Throws<InvalidDataException>(() => ClientUpdater.ExtractVerified(zip, Path.Combine(_root, "staging"), files, null, CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "outside.txt")));
        }

        [Theory]
        [InlineData("../x")]
        [InlineData(@"..\x")]
        [InlineData("a/../../x")]
        public void ResolveInside_Blocks_Escapes(string rel)
        {
            Assert.Throws<InvalidDataException>(() => ClientUpdater.ResolveInside(_root, rel));
        }

        private string CreateZip(params (string name, string content)[] entries)
        {
            string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");

            using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                foreach (var (name, content) in entries)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                    writer.Write(content);
                }
            }

            return path;
        }

        private static string Md5(string content)
        {
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        }
    }
}
