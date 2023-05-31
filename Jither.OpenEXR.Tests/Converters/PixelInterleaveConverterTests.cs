using Jither.OpenEXR.Drawing;
using System.Text;

namespace Jither.OpenEXR.Converters;
public class PixelInterleaveConverterTests
{
    private static byte[] StringToPixelData(string str)
    {
        // For (semi)readability, we're using ASCII characters here.
        // PixelInterleaveConverter works on byte arrays and simply rearranges them - it doesn't care that they're not floats.
        return Encoding.ASCII.GetBytes(str.Replace(" ", ""));
    }

    [Fact]
    public void Converts_from_RGBA_to_EXR_Half()
    {
        // RGBA RGBA RGBA RGBA
        byte[] source = StringToPixelData("AB CD EF GH   IJ KL MN OP   QR ST UV WX   YZ 01 23 45");
        byte[] dest = new byte[source.Length];
        var channelList = ChannelList.CreateRGBAHalf();

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.ToEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // AAAA BBBB GGGG RRRR
        var expected = StringToPixelData("GH OP WX 45   EF MN UV 23   CD KL ST 01   AB IJ QR YZ");
        
        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Converts_from_EXR_to_RGBA_Half()
    {
        // AAAA BBBB GGGG RRRR
        byte[] source = StringToPixelData("GH OP WX 45   EF MN UV 23   CD KL ST 01   AB IJ QR YZ");
        byte[] dest = new byte[source.Length];
        var channelList = ChannelList.CreateRGBAHalf();

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.FromEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // RGBA RGBA RGBA RGBA
        var expected = StringToPixelData("AB CD EF GH   IJ KL MN OP   QR ST UV WX   YZ 01 23 45");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Converts_from_RGBA_to_EXR_Float()
    {
        // RGBA RGBA RGBA RGBA
        byte[] source = StringToPixelData("ABCD EFGH IJKL MNOP   QRST UVWX YZ01 2345   6789 abcd efgh ijkl   mnop qrst uvwx yz!?");
        byte[] dest = new byte[source.Length];
        var channelList = ChannelList.CreateRGBAFloat();

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.ToEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // AAAA BBBB GGGG RRRR
        var expected = StringToPixelData("MNOP 2345 ijkl yz!?   IJKL YZ01 efgh uvwx   EFGH UVWX abcd qrst  ABCD QRST 6789 mnop");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Converts_from_EXR_to_RGBA_Float()
    {
        // AAAA BBBB GGGG RRRR
        byte[] source = StringToPixelData("MNOP 2345 ijkl yz!?   IJKL YZ01 efgh uvwx   EFGH UVWX abcd qrst  ABCD QRST 6789 mnop");
        byte[] dest = new byte[source.Length];
        var channelList = ChannelList.CreateRGBAFloat();

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.FromEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // RGBA RGBA RGBA RGBA
        var expected = StringToPixelData("ABCD EFGH IJKL MNOP   QRST UVWX YZ01 2345   6789 abcd efgh ijkl   mnop qrst uvwx yz!?");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Converts_multiple_scanlines_to_EXR()
    {
        // TODO: This test may be a bit minimal... 2x2 pixels
        // RGBA RGBA
        // RGBA RGBA
        byte[] source = StringToPixelData("AB CD EF GH   IJ KL MN OP   QR ST UV WX   YZ 01 23 45");
        byte[] dest = new byte[source.Length];
        var channelList = ChannelList.CreateRGBAHalf();

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.ToEXR(new Bounds<int>(0, 0, 2, 2), source, dest, 0);

        // AA BB GG RR
        // AA BB GG RR
        var expected = StringToPixelData("GH OP   EF MN   CD KL   AB IJ   WX 45   UV 23   ST 01   QR YZ");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Converts_multiple_scanlines_from_EXR()
    {
        // TODO: This test may be a bit minimal... 2x2 pixels
        // AA BB GG RR
        // AA BB GG RR
        byte[] source = StringToPixelData("GH OP   EF MN   CD KL   AB IJ   WX 45   UV 23   ST 01   QR YZ");
        byte[] dest = new byte[source.Length];
        var channelList = ChannelList.CreateRGBAHalf();

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.FromEXR(new Bounds<int>(0, 0, 2, 2), source, dest, 0);

        // RGBA RGBA
        // RGBA RGBA
        var expected = StringToPixelData("AB CD EF GH   IJ KL MN OP   QR ST UV WX   YZ 01 23 45");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Can_remove_channels_Half()
    {
        // AAAA BBBB GGGG RRRR
        byte[] source = StringToPixelData("GHOPWX45 EFMNUV23 CDKLST01 ABIJQRYZ");
        byte[] dest = new byte[source.Length - 8]; // No alpha channel = subtract 8 bytes (one half per pixel)
        var channelList = ChannelList.CreateRGBAHalf();
        
        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B");
        converter.FromEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // RGB RGB RGB RGB
        var expected = StringToPixelData("AB CD EF   IJ KL MN   QR ST UV   YZ 01 23");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Can_remove_channels_Float()
    {
        // AAAA BBBB GGGG RRRR
        byte[] source = StringToPixelData("MNOP 2345 ijkl yz!?   IJKL YZ01 efgh uvwx   EFGH UVWX abcd qrst  ABCD QRST 6789 mnop");
        byte[] dest = new byte[source.Length - 16]; // No alpha channel = subtract 16 bytes (one float per pixel)
        var channelList = ChannelList.CreateRGBAFloat();

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B");
        converter.FromEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // RGB RGB RGB RGB
        var expected = StringToPixelData("ABCD EFGH IJKL   QRST UVWX YZ01   6789 abcd efgh   mnop qrst uvwx");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Converts_mixed_to_EXR()
    {
        // RGBA RGBA RGBA RGBA - blue is 32-bit
        byte[] source = StringToPixelData("AB CD EFGH IJ   KL MN OPQR ST   UV WX YZ01 23   45 67 89ab cd");
        byte[] dest = new byte[source.Length];
        var channelList = new ChannelList
        {
            Channel.A_Half,
            Channel.B_Float,
            Channel.G_Half,
            Channel.R_Half
        };

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.ToEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // AAAA BBBB GGGG RRRR
        var expected = StringToPixelData("IJ ST 23 cd   EFGH OPQR YZ01 89ab   CD MN WX 67   AB KL UV 45");

        Assert.Equal(expected, dest);
    }

    [Fact]
    public void Converts_mixed_from_EXR()
    {
        // AAAA BBBB GGGG RRRR - blue is 32-bit
        byte[] source = StringToPixelData("IJ ST 23 cd   EFGH OPQR YZ01 89ab   CD MN WX 67   AB KL UV 45");
        byte[] dest = new byte[source.Length];
        var channelList = new ChannelList
        {
            Channel.A_Half,
            Channel.B_Float,
            Channel.G_Half,
            Channel.R_Half
        };

        var converter = new PixelInterleaveConverter(channelList, "R", "G", "B", "A");
        converter.FromEXR(new Bounds<int>(0, 0, 4, 1), source, dest, 0);

        // RGBA RGBA RGBA RGBA
        var expected = StringToPixelData("AB CD EFGH IJ   KL MN OPQR ST   UV WX YZ01 23   45 67 89ab cd");

        Assert.Equal(expected, dest);
    }
}
