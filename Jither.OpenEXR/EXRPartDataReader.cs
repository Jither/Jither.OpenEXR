using Jither.OpenEXR.Compression;
using System.Buffers;

namespace Jither.OpenEXR;

public class EXRPartDataReader : EXRPartDataHandler
{
    private OffsetTable? _offsets;

    private readonly EXRReader reader;
    private readonly long offsetTableOffset;

    private OffsetTable OffsetTable
    {
        get
        {
            if (_offsets != null)
            {
                return _offsets;
            }

            reader.Seek(offsetTableOffset);
            _offsets = OffsetTable.ReadFrom(reader, ChunkCount);
            return _offsets;
        }
    }

    public EXRPartDataReader(EXRPart part, EXRVersion version, EXRReader reader, long offsetTableOffset) : base(part, version)
    {
        this.reader = reader;
        this.offsetTableOffset = offsetTableOffset;
    }

    public void Read(byte[] dest)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        if (dest == null)
        {
            throw new ArgumentNullException(nameof(dest));
        }

        var totalBytes = GetTotalByteCount();
        if (dest.Length < totalBytes)
        {
            throw new ArgumentException($"Destination array too small ({dest.Length}) to fit pixel data ({totalBytes})");
        }

        int destOffset = 0;
        for (int i = 0; i < ChunkCount; i++)
        {
            var chunkInfo = ReadChunkHeader(i);
            InternalReadChunk(chunkInfo, dest, destOffset);
            destOffset += chunkInfo.UncompressedByteCount;
        }
    }

    public void ReadInterleaved(byte[] dest, IEnumerable<string> channelOrder)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        CheckInterleavedPrerequisites();

        if (dest == null)
        {
            throw new ArgumentNullException(nameof(dest));
        }

        int offset = 0;
        for (int i = 0; i < ChunkCount; i++)
        {
            ChunkInfo chunkInfo = InternalReadChunkInterleaved(i, dest, channelOrder, offset);
            offset += chunkInfo.UncompressedByteCount;
        }
    }

    public void ReadChunk(int chunkIndex, byte[] dest, int destOffset = 0)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        var chunkInfo = ReadChunkHeader(chunkIndex);
        InternalReadChunk(chunkInfo, dest, destOffset);
    }

    public ChunkInfo ReadChunkInterleaved(int chunkIndex, byte[] dest, IEnumerable<string> channelOrder, int offset = 0)
    {
        part.ValidateAttributes(fileIsMultiPart, fileHasDeepData);

        CheckInterleavedPrerequisites();

        return InternalReadChunkInterleaved(chunkIndex, dest, channelOrder, offset);
    }

    private ChunkInfo InternalReadChunkInterleaved(int chunkIndex, byte[] dest, IEnumerable<string> channelOrder, int offset)
    {
        var chunkInfo = ReadChunkHeader(chunkIndex);

        // Offsets are always ordered with scanlines from top to bottom (INCREASING_Y). However, the order of the scanlines themselves within the file
        // may be bottom to top or random (see LineOrder). Each scanline block stores its first scanline's y coordinate, meaning it's possible to
        // read blocks in file sequential order and reconstruct the scanline order - avoiding file seeks. For now, we just follow the
        // offset order.

        // Collect byte offsets for the channel components in each pixel. I.e., at what byte offset within the channel-interleaved pixel should each channel be stored?
        var destOffsets = GetInterleaveOffsets(channelOrder, out var interleavedBytesPerPixel);

        var pixelData = ArrayPool<byte>.Shared.Rent(GetChunkByteCount(chunkInfo));
        try
        {
            InternalReadChunk(chunkInfo, pixelData, 0);

            // The decompressed pixel data is stored with channels separated and ordered alphabetically
            int sourceOffset = 0;
            int scanlineCount = GetChunkScanLineCount(chunkInfo);

            for (int scanline = 0; scanline < scanlineCount; scanline++)
            {
                int channelIndex = 0;
                int scanlineOffset = offset + scanline * PixelsPerScanLine * interleavedBytesPerPixel;

                foreach (var channel in part.Channels)
                {
                    int channelBytesPerPixel = channel.Type.GetBytesPerPixel();
                    int destOffset = destOffsets[channelIndex++];

                    if (destOffset >= 0)
                    {
                        destOffset += scanlineOffset;
                        for (int i = 0; i < PixelsPerScanLine; i++)
                        {
                            for (int j = 0; j < channelBytesPerPixel; j++)
                            {
                                dest[destOffset + j] = pixelData[sourceOffset++];
                            }
                            destOffset += interleavedBytesPerPixel;
                        }
                    }
                    else
                    {
                        // Skip this channel
                        sourceOffset += PixelsPerScanLine * channelBytesPerPixel;
                    }
                }
            }

            return chunkInfo;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixelData);
        }
    }

    private ChunkInfo ReadChunkHeader(int chunkIndex)
    {
        var offset = OffsetTable[chunkIndex];

        reader.Seek(offset);
        int partNumber = fileIsMultiPart ? reader.ReadInt() : 0;

        ChunkInfo chunkInfo;
        if (IsTiled)
        {
            if (reader.Remaining < 16)
            {
                throw new EXRFormatException($"Truncated chunk header - expected at least 16 bytes, was: {reader.Remaining}");
            }
            int x = reader.ReadInt();
            int y = reader.ReadInt();
            int levelX = reader.ReadInt();
            int levelY = reader.ReadInt();
            chunkInfo = new TileChunkInfo(chunkIndex, partNumber, x, y, levelX, levelY);
        }
        else
        {
            if (reader.Remaining < 4)
            {
                throw new EXRFormatException($"Truncated chunk header - expected at least 4 bytes, was: {reader.Remaining}");
            }
            int y = reader.ReadInt();
            chunkInfo = new ScanlineChunkInfo(chunkIndex, partNumber, y);
        }

        chunkInfo.CompressedByteCount = reader.ReadInt();
        chunkInfo.UncompressedByteCount = GetChunkByteCount(chunkInfo);

        chunkInfo.PixelDataFileOffset = reader.Position;
        chunkInfo.FileOffset = offset;

        return chunkInfo;
    }

    private void InternalReadChunk(ChunkInfo chunkInfo, byte[] dest, int destIndex)
    {
        reader.Seek(chunkInfo.PixelDataFileOffset);
        var chunkStream = reader.GetChunkStream(chunkInfo.CompressedByteCount);

        if (destIndex + chunkInfo.UncompressedByteCount > dest.Length)
        {
            throw new EXRFormatException($"Uncompressed byte count for {chunkInfo} ({chunkInfo.UncompressedByteCount}) exceeds expected size (max {dest.Length - destIndex}");
        }

        using (var destStream = new MemoryStream(dest, destIndex, chunkInfo.UncompressedByteCount))
        {
            // Yes, compressors could use the length or capacity of the stream rather than
            // an explicit expectedBytes parameter, but not sure if we'll change this
            // implementation in the future.
            var info = new PixelDataInfo(
                part.Channels,
                GetBounds(chunkInfo),
                chunkInfo.UncompressedByteCount
            );

            compressor.Decompress(chunkStream, destStream, info);
        }
    }
}
