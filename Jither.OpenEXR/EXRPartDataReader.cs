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
            _offsets = OffsetTable.ReadFrom(reader, chunkCount);
            return _offsets;
        }
    }

    public EXRPartDataReader(EXRPart part, EXRVersion version, EXRReader reader) : base(part, version)
    {
        this.reader = reader;
        this.offsetTableOffset = reader.Position;
    }

    public void Read(byte[] dest)
    {
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
        for (int i = 0; i < chunkCount; i++)
        {
            int bytesRead = InternalReadBlock(i, dest, destOffset);
            destOffset += bytesRead;
        }
    }

    public void ReadInterleaved(byte[] dest, IEnumerable<string> channelOrder)
    {
        if (dest == null)
        {
            throw new ArgumentNullException(nameof(dest));
        }

        int offset = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            int bytesRead = ReadBlockInterleaved(i, dest, channelOrder, offset);
            offset += bytesRead;
        }
    }

    public void ReadBlock(int chunkIndex, byte[] dest, int destOffset = 0)
    {
        InternalReadBlock(chunkIndex, dest, destOffset);
    }

    public int ReadBlockInterleaved(int chunkIndex, byte[] dest, IEnumerable<string> channelOrder, int offset = 0)
    {
        CheckInterleavedPrerequisites();

        // Offsets are always ordered with scanlines from top to bottom (INCREASING_Y). However, the order of the scanlines themselves within the file
        // may be bottom to top or random (see LineOrder). Each block stores its first scanline's y coordinate, meaning it's possible to
        // read blocks in file sequential order and reconstruct the scanline order - avoiding file seeks. For now, we just follow the
        // offset order.

        // Collect byte offsets for the channel components in each pixel. I.e., at what byte offset within the channel-interleaved pixel should each channel be stored?
        var destOffsets = GetInterleaveOffsets(channelOrder, out var interleavedBytesPerPixel);

        var data = ArrayPool<byte>.Shared.Rent(GetBlockByteCount(chunkIndex));
        try
        {
            int bytesRead = InternalReadBlock(chunkIndex, data, 0);

            // The decompressed pixel data is stored with channels separated and ordered alphabetically
            int sourceOffset = 0;
            int scanlineCount = GetBlockScanLineCount(chunkIndex);

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
                                dest[destOffset + j] = data[sourceOffset++];
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

            return bytesRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    private int InternalReadBlock(int chunkIndex, byte[] dest, int destIndex)
    {
        var offset = OffsetTable[chunkIndex];
        reader.Seek((long)offset);
        int partNumber = isMultiPart ? reader.ReadInt() : 0;
        int y = reader.ReadInt();
        int pixelDataSize = reader.ReadInt();

        var chunkStream = reader.GetChunkStream(pixelDataSize);
        int bytesToRead = GetBlockByteCount(chunkIndex);
        using (var destStream = new MemoryStream(dest, destIndex, bytesToRead))
        {
            // Yes, compressors could use the length or capacity of the stream rather than
            // an explicit expectedBytes parameter, but not sure if we'll change this
            // implementation in the future.
            var info = new PixelDataInfo(
                part.Channels, 
                new System.Drawing.Rectangle(0, y, PixelsPerScanLine, GetBlockScanLineCount(chunkIndex)),
                bytesToRead
            );

            compressor.Decompress(chunkStream, destStream, info);
        }

        return bytesToRead;
    }
}
