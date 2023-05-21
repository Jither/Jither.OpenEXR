using Jither.OpenEXR.Compression;

namespace Jither.OpenEXR;

public abstract class EXRPartDataHandler
{
    protected readonly EXRPart part;
    protected readonly Compressor compressor;
    protected int PixelsPerBlock => compressor.ScanLinesPerBlock * part.DataWindow.Width;
    protected int BytesPerPixel => part.Channels.Sum(c => c.Type.GetBytesPerPixel());
    protected int BitsPerPixel => BytesPerPixel * 8;
    public int BytesPerBlock => BytesPerPixel * PixelsPerBlock;
    public int BytesInLastBlock {
        get
        {
            int scanlines = part.DataWindow.Height % compressor.ScanLinesPerBlock;
            if (scanlines == 0)
            {
                scanlines = compressor.ScanLinesPerBlock;
            }
            return part.DataWindow.Width * scanlines * BytesPerPixel;
        }
    }
    public int TotalBytes => part.DataWindow.Width * part.DataWindow.Height * BytesPerPixel;

    protected EXRPartDataHandler(EXRPart part)
    {
        this.part = part;
        compressor = part.Compression switch
        {
            EXRCompression.None => new NullCompressor(),
            EXRCompression.RLE => new RLECompressor(),
            EXRCompression.ZIPS => new ZipSCompressor(),
            EXRCompression.ZIP => new ZipCompressor(),
            EXRCompression.PIZ => new PizCompressor(),
            _ => throw new NotSupportedException($"{part.Compression} compression not supported")
        };
    }

    protected List<int> GetInterleaveOffsets(IEnumerable<string> channelOrder, out int bytesPerPixel, bool allChannelsRequired = false)
    {
        var channels = part.Channels;
        var offsets = new List<int>(channels.Count);

        for (int i = 0; i < channels.Count; i++)
        {
            offsets.Add(-1);
        }

        int startOffset = 0;
        foreach (var outputChannel in channelOrder)
        {
            var channelIndex = channels.IndexOf(outputChannel);
            if (channelIndex < 0)
            {
                throw new ArgumentException($"Unknown channel name in interleaved channel order: {outputChannel}. Should be one of: {String.Join(", ", channels.Select(c => c.Name))}", nameof(channelOrder));
            }
            var inputChannel = channels[channelIndex];
            offsets[channelIndex] = startOffset;
            startOffset += inputChannel.Type.GetBytesPerPixel();
        }

        if (allChannelsRequired)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                if (offsets[i] < 0)
                {
                    throw new ArgumentException($"Channel order for interleaved block is missing channel '{channels[i].Name}'.", nameof(channelOrder));
                }
            }
        }

        // startOffset is now also "magically" the number of bytes per interleaved pixel
        bytesPerPixel = startOffset;
        return offsets;
    }
}
