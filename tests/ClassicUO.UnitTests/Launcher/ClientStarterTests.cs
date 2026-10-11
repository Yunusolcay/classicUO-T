using System.Text.Json.Nodes;
using ClassicUO.Launcher.Core;
using ClassicUO.Utility;
using Xunit;

namespace ClassicUO.UnitTests.Launcher
{
    public class ClientStarterTests
    {
        [Fact]
        public void Launcher_Password_Format_Matches_ClassicUO()
        {
            string enc = ClassicUOCrypter.Encrypt("s3cret!");

            Assert.Equal(Crypter.Encrypt("s3cret!"), enc);
            Assert.Equal("s3cret!", Crypter.Decrypt(enc));
            Assert.Equal("s3cret!", ClassicUOCrypter.Decrypt(enc));
        }

        [Fact]
        public void Protected_Password_Round_Trips()
        {
            string stored = PasswordProtector.Protect("pässword");

            Assert.DoesNotContain("pässword", stored);
            Assert.Equal("pässword", PasswordProtector.Unprotect(stored));
            Assert.Equal(string.Empty, PasswordProtector.Unprotect("dpapi:not-base64!"));
        }

        [Fact]
        public void Aes_Fallback_Round_Trips_Unicode_And_Detects_Tampering()
        {
            // the Linux/macOS path; runs on every OS
            string stored = PasswordProtector.ProtectAes(System.Text.Encoding.UTF8.GetBytes("pässwörd ğış"));

            Assert.StartsWith("aes:", stored);
            Assert.Equal("pässwörd ğış", PasswordProtector.Unprotect(stored));

            byte[] blob = System.Convert.FromBase64String(stored.Substring(4));
            blob[^1] ^= 0xFF;
            Assert.Equal(string.Empty, PasswordProtector.Unprotect("aes:" + System.Convert.ToBase64String(blob)));
        }

        [Fact]
        public void Merge_Keeps_ClassicUO_Own_Settings()
        {
            const string existing = """{ "fps": 144, "window_size": "1280,720", "ip": "old", "username": "keep", "password": "1-ABCD", "saveaccount": true }""";
            var profile = new ServerProfile { Host = " uo.example.com ", Port = 2600, UltimaOnlineDirectory = @"C:\UO", Plugins = { "./Assistant/Razor.dll", " " } };

            JsonObject json = ClientStarter.MergeSettings(existing, profile);

            Assert.Equal(144, (int)json["fps"]!);
            Assert.Equal("1280,720", (string)json["window_size"]!);
            Assert.Equal("uo.example.com", (string)json["ip"]!);
            Assert.Equal(2600, (int)json["port"]!);
            Assert.Single(json["plugins"]!.AsArray());
            // password not saved in the launcher: what the player saved in ClassicUO stays
            Assert.Equal("keep", (string)json["username"]!);
            Assert.Equal("1-ABCD", (string)json["password"]!);
            Assert.True((bool)json["saveaccount"]!);
        }

        [Fact]
        public void Merge_Writes_Saved_Password_In_ClassicUO_Format()
        {
            var profile = new ServerProfile { Username = "acc", SavePassword = true, ProtectedPassword = PasswordProtector.Protect("pw"), AutoLogin = true };

            JsonObject json = ClientStarter.MergeSettings(null, profile);

            Assert.Equal("pw", Crypter.Decrypt((string)json["password"]!));
            Assert.True((bool)json["autologin"]!);
        }

        [Fact]
        public void Turning_Save_Password_Off_Clears_It_Once()
        {
            const string existing = """{ "password": "1-ABCD", "saveaccount": true, "autologin": true }""";
            var profile = new ServerProfile { SavePassword = false, ClearClientPassword = true };

            JsonObject json = ClientStarter.MergeSettings(existing, profile);

            Assert.Equal(string.Empty, (string)json["password"]!);
            Assert.False((bool)json["autologin"]!);
            Assert.False(profile.ClearClientPassword);
        }

        [Fact]
        public void Merge_Survives_Corrupt_File()
        {
            JsonObject json = ClientStarter.MergeSettings("{ not json", new ServerProfile());

            Assert.Equal("127.0.0.1", (string)json["ip"]!);
        }

        [Theory]
        [InlineData("", new string[0])]
        [InlineData("-fps 144", new[] { "-fps", "144" })]
        [InlineData("-uopath \"C:\\Program Files\\UO\"  -debug", new[] { "-uopath", "C:\\Program Files\\UO", "-debug" })]
        [InlineData("-x \"\"", new[] { "-x", "" })]
        public void Splits_Extra_Arguments(string text, string[] expected)
        {
            Assert.Equal(expected, ClientStarter.SplitArguments(text));
        }

        [Fact]
        public void Config_Round_Trips_And_Defaults_To_SphereNet()
        {
            LauncherConfig? parsed = LauncherConfig.Parse("""{ "Profiles": [ { "Name": "Test", "Port": 2600 } ], "UpdateManifestUrl": "https://x/manifest.xml" }""");

            Assert.NotNull(parsed);
            Assert.Equal("Test", parsed!.Profiles[0].Name);
            Assert.Equal(2600, parsed.Profiles[0].Port);
            Assert.Equal("ClassicUO", parsed.ClientDirectory);
            Assert.Equal("SphereNet", new ServerProfile().Name);
            Assert.Equal(2593, new ServerProfile().Port);
        }
    }
}
