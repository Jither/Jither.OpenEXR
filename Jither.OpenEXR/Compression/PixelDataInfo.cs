using Rectangle = System.Drawing.Rectangle;

namespace Jither.OpenEXR.Compression;

public class PixelDataInfo
{
    public ChannelList Channels { get; }
    public Rectangle Bounds { get; }
    public int UncompressedByteSize { get; }

    public PixelDataInfo(ChannelList channels, Rectangle bounds, int expectedByteSize)
    {
        Channels = channels;
        Bounds = bounds;
        UncompressedByteSize = expectedByteSize;
    }
}
