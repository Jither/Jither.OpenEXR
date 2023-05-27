using Jither.OpenEXR.Compression;

namespace Jither.OpenEXR;

public abstract class EXRPartDataHandler
{
    protected readonly EXRPart part;
    protected readonly Compressor compressor;
    protected readonly int chunkCount;
    protected readonly bool isMultiPart;

    protected int PixelsPerScanLine => part.DataWindow.Width;

    public int GetTotalByteCount()
    {
        if (chunkCount > 1)
        {
            // <chunkCount> full blocks + possibly smaller last block
            return GetBlockByteCount(0) * (chunkCount - 1) + GetBlockByteCount(chunkCount - 1);
        }
        return GetBlockByteCount(0);
    }

    protected void CheckInterleavedPrerequisites()
    {
        if (part.Channels.AreSubsampled)
        {
            throw new NotSupportedException($"Interleaved read/write is not supported with subsampled channels.");
        }
    }

    protected int GetBlockByteCount(int chunkIndex)
    {
        var scanlines = GetBlockScanLineCount(chunkIndex);
        return part.Channels.GetByteCount(new Attributes.V2i(PixelsPerScanLine, scanlines));
    }

    protected int GetBlockScanLineCount(int chunkIndex)
    {
        if (chunkIndex < chunkCount - 1)
        {
            return compressor.ScanLinesPerBlock;
        }
        // Last block may not have the full set:
        int scanlines = part.DataWindow.Height % compressor.ScanLinesPerBlock;
        if (scanlines == 0)
        {
            scanlines = compressor.ScanLinesPerBlock;
        }
        return scanlines;
    }

    protected int GetBlockPixelCount(int chunkIndex)
    {
        int scanlines = GetBlockScanLineCount(chunkIndex);
        return part.DataWindow.Width * scanlines;
    }

    protected EXRPartDataHandler(EXRPart part, EXRVersion version)
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
        
        isMultiPart = version.IsMultiPart;

        if (isMultiPart)
        {
            chunkCount = part.GetAttributeOrThrow<int>("chunkCount");
        }
        else if (version.IsSinglePartTiled)
        {
            // TODO: offsetTable for single part tiled
            throw new NotImplementedException($"Reading of single part tiled images is not implemented.");
        }
        else
        {
            chunkCount = (int)Math.Ceiling((double)part.DataWindow.Height / compressor.ScanLinesPerBlock);
        }
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
