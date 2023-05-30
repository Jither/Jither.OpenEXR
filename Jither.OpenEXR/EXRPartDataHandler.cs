using Jither.OpenEXR.Attributes;
using Jither.OpenEXR.Compression;
using Jither.OpenEXR.Drawing;
using Jither.OpenEXR.Helpers;
using System.Drawing;

namespace Jither.OpenEXR;

public abstract class EXRPartDataHandler
{
    protected readonly EXRPart part;
    protected readonly Compressor compressor;
    public int ChunkCount { get; }
    protected readonly bool fileIsMultiPart;
    protected readonly bool fileHasDeepData;

    protected int PixelsPerScanLine => part.DataWindow.Width;

    /// <summary>
    /// Returns the number of bytes needed to contain the part's complete pixel data.
    /// </summary>
    /// <remarks>
    /// This will throw <see cref="EXRFormatException"/> for byte counts above 2GB. A .NET array wouldn't be able to hold the complete image.
    /// OpenEXR limits individual chunks to 2GB, but has no such limitation for the complete image. In order to read an image larger than this, use
    /// <seealso cref="GetTotalByteCountLarge"/> and process each chunk individually.
    /// </remarks>
    public int GetTotalByteCount()
    {
        try
        {
            var bounds = part.DataWindow.ToBounds();
            return part.Channels.GetByteCount(bounds);
        }
        catch (OverflowException ex)
        {
            throw new EXRFormatException($"Byte count of part '{part.Name}' exceeds 2GB", ex);
        }
    }

    /// <summary>
    /// Returns the number of bytes needed to contain the part's complete pixel data. Unlike <see cref="GetTotalByteCount"/>, this allows computing
    /// sizes (way) larger than 2GB.
    /// </summary>
    /// <returns></returns>
    public ulong GetTotalByteCountLarge()
    {
        var bounds = part.DataWindow.ToBounds();
        return part.Channels.GetByteCountLarge(bounds);
    }

    protected void CheckInterleavedPrerequisites()
    {
        if (part.Channels.AreSubsampled)
        {
            throw new NotSupportedException($"Interleaved read/write is not supported with subsampled channels.");
        }
    }

    protected int GetChunkByteCount(ChunkInfo chunkInfo)
    {
        var bounds = GetBounds(chunkInfo);
        try
        {
            return part.Channels.GetByteCount(bounds);
        }
        catch (OverflowException ex)
        {
            throw new EXRFormatException($"Combined byte count of {chunkInfo} exceeds 2GB", ex);
        }
    }

    protected int GetChunkScanLineCount(ChunkInfo chunkInfo)
    {
        if (chunkInfo.Index < ChunkCount - 1)
        {
            return compressor.ScanLinesPerChunk;
        }
        // Last chunk may not have the full set:
        int scanlines = part.DataWindow.Height % compressor.ScanLinesPerChunk;
        if (scanlines == 0)
        {
            scanlines = compressor.ScanLinesPerChunk;
        }
        return scanlines;
    }

    protected int GetChunkPixelCount(ChunkInfo chunkInfo)
    {
        int scanlines = GetChunkScanLineCount(chunkInfo);
        return part.DataWindow.Width * scanlines;
    }

    protected bool IsTiled => part.IsTiled;

    protected Bounds<int> GetBounds(ChunkInfo chunkInfo)
    {
        if (chunkInfo is ScanlineChunkInfo scanline)
        {
            return new Bounds<int>(0, scanline.Y, PixelsPerScanLine, GetChunkScanLineCount(chunkInfo));
        }
        else if (chunkInfo is TileChunkInfo tile)
        {
            var tileDesc = part.Tiles ?? throw new InvalidOperationException($"Expected part to have a tiles attribute.");
            var dataWindow = part.DataWindow;
            int width = Math.Min(tileDesc.XSize, dataWindow.Width - tile.X);
            int height = Math.Min(tileDesc.YSize, dataWindow.Height - tile.Y);
            return new Bounds<int>(tile.X, tile.Y, width, height);
        }
        throw new NotImplementedException($"Expected chunk info to be scanline or tile");
    }

    protected EXRPartDataHandler(EXRPart part, EXRVersion version)
    {
        this.part = part;

        fileIsMultiPart = version.IsMultiPart;
        fileHasDeepData = version.HasNonImageParts;

        compressor = part.Compression switch
        {
            EXRCompression.None => new NullCompressor(),
            EXRCompression.RLE => new RLECompressor(),
            EXRCompression.ZIPS => new ZipSCompressor(),
            EXRCompression.ZIP => new ZipCompressor(),
            EXRCompression.PIZ => new PizCompressor(),
            _ => new UnsupportedCompressor(part.Compression)
        };
        
        if (fileIsMultiPart)
        {
            ChunkCount = part.GetAttributeOrThrow<int>("chunkCount");
        }
        else if (version.IsSinglePartTiled)
        {
            var tiles = part.Tiles ?? throw new EXRFormatException($"Missing tiles attribute for single tiled part");
            // "In a file with multiple levels, tiles have the same size, regardless of their level. Lower-resolution levels contain fewer, rather than smaller, tiles."
            // So, we need to figure out the number of tiles required to cover DataWindow at each level.
            ChunkCount = 0;
            int totalWidth = part.DataWindow.Width;
            int totalHeight = part.DataWindow.Height;
            foreach (var coverage in tiles.Coverages)
            {
                ChunkCount += MathHelpers.DivAndRoundUp(totalWidth, coverage.Width) * MathHelpers.DivAndRoundUp(totalHeight, coverage.Height);
            }
        }
        else
        {
            // Tiled files are either multi-part - in which case they must have an explicit chunkCount attribute,
            // or they have a single-part-tiled version flag, handled above. Hence, this must be a scanline part:
            ChunkCount = MathHelpers.DivAndRoundUp(part.DataWindow.Height, compressor.ScanLinesPerChunk);
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
                    throw new ArgumentException($"Channel order for interleaved chunk is missing channel '{channels[i].Name}'.", nameof(channelOrder));
                }
            }
        }

        // startOffset is now also "magically" the number of bytes per interleaved pixel
        bytesPerPixel = startOffset;
        return offsets;
    }
}
