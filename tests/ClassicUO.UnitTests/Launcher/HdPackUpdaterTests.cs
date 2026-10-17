using System.Text.Json;
using ClassicUO.Launcher.Core;
using Xunit;

namespace ClassicUO.UnitTests.Launcher
{
    public sealed class HdPackUpdaterTests
    {
        [Theory]
        [InlineData("Yunusolcay/classicUO-T-HD-pack", true)]
        [InlineData("owner/name.with_dots-1", true)]
        [InlineData("", false)]
        [InlineData("owner", false)]
        [InlineData("owner/name/extra", false)]
        [InlineData("owner/..", false)]
        [InlineData("owner/na me", false)]
        [InlineData("../name", false)]
        public void Validates_Repository(string repository, bool valid)
        {
            Assert.Equal(valid, HdPackUpdater.IsValidRepository(repository));
        }

        [Fact]
        public void Needs_Repository_And_Token()
        {
            Assert.False(HdPackUpdater.IsConfigured("owner/name", " "));
            Assert.False(HdPackUpdater.IsConfigured("", "token"));
            Assert.True(HdPackUpdater.IsConfigured("owner/name", "token"));
        }

        [Fact]
        public void Finds_Api_Url_Of_Asset()
        {
            using JsonDocument doc = JsonDocument.Parse("""
                {"assets": [
                  {"name": "hd-world.zip", "url": "https://api.github.com/repos/o/r/releases/assets/2"},
                  {"name": "manifest.xml", "url": "https://api.github.com/repos/o/r/releases/assets/1"},
                  {"name": "evil.xml", "url": "https://example.com/evil"}
                ]}
                """);

            Assert.Equal("https://api.github.com/repos/o/r/releases/assets/1", HdPackUpdater.FindAsset(doc.RootElement, "manifest.xml")?.AbsoluteUri);
            Assert.Null(HdPackUpdater.FindAsset(doc.RootElement, "evil.xml"));
            Assert.Null(HdPackUpdater.FindAsset(doc.RootElement, "missing.zip"));
        }
    }
}
