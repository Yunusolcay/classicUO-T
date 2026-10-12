using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.Game.HdWorld
{
    public class HdWorldPackTests
    {
        [Fact]
        public void Halve_AveragesEachBlockPerChannel()
        {
            // 4x2 RGBA (premultiplied, A in the high byte): two 2x2 blocks
            uint[] src =
            {
                0xFF000000, 0xFF0000FF, 0x00000000, 0x00000000,
                0xFF00FF00, 0xFFFF0000, 0x00000000, 0x00000000
            };

            uint[] dst = HdWorldPack.Halve(src, 4, 2);

            Assert.Equal(2, dst.Length);
            // each color channel: one 255 out of four -> 64 (rounded); alpha stays 255
            Assert.Equal(0xFF404040u, dst[0]);
            // transparent block stays fully transparent, no halo
            Assert.Equal(0u, dst[1]);
        }

        [Fact]
        public void Halve_KeepsGrayGray()
        {
            uint[] src = { 0xFF101010, 0xFF202020, 0xFF303030, 0xFF404040 };

            uint p = HdWorldPack.Halve(src, 2, 2)[0];

            Assert.Equal(p & 0xFF, (p >> 8) & 0xFF);
            Assert.Equal(p & 0xFF, (p >> 16) & 0xFF);
        }
    }
}
