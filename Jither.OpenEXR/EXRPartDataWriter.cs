using Jither.OpenEXR.Compression;
using System.Buffers;

namespace Jither.OpenEXR;

public class EXRPartDataWriter : EXRPartDataHandler
{
    private readonly EXRWriter writer;
    private readonly long offsetTableOffset;

    internal EXRPartDataWriter(EXRPart part, EXRVersion version, EXRWriter writer) : base(part, version)
    {
        this.writer = writer;
        offsetTableOffset = writer.Position;
    }

    public void WriteOffsetPlaceholders()
    {
        for (int i = 0; i < ChunkCount; i++)
        {
            writer.WriteULong(0xffffffffffffffffUL);
        }
    }

    public void Write(byte[] data)
    {
        int sourceOffset = 0;
        for (int chunkIndex = 0; chunkIndex < ChunkCount; chunkIndex++)
        {
            int y = chunkIndex * compressor.ScanLinesPerChunk + part.DataWindow.YMin;
            var chunkInfo = new ScanlineChunkInfo(chunkIndex, part.PartNumber, y);
            int bytesWritten = InternalWriteChunk(chunkInfo, data, sourceOffset);
            sourceOffset += bytesWritten;
        }
    }

    public void WriteInterleaved(byte[] data, IEnumerable<string> channelOrder)
    {
        int sourceOffset = 0;
        for (int chunkIndex = 0; chunkIndex < ChunkCount; chunkIndex++)
        {
            int y = chunkIndex * compressor.ScanLinesPerChunk + part.DataWindow.YMin;
            var chunkInfo = new ScanlineChunkInfo(chunkIndex, part.PartNumber, y);
            int bytesWritten = WriteChunkInterleaved(chunkInfo, data, channelOrder, sourceOffset);
            sourceOffset += bytesWritten;
        }
    }

    public void WriteChunk(ChunkInfo chunkInfo, byte[] data, int offset = 0)
    {
        CheckWriteCount(chunkInfo, data, offset);
        InternalWriteChunk(chunkInfo, data, offset);
    }

    public int WriteChunkInterleaved(ChunkInfo chunkInfo, byte[] data, IEnumerable<string> channelOrder, int offset = 0)
    {
        CheckWriteCount(chunkInfo, data, offset);
        CheckInterleavedPrerequisites();
        var pixelData = ArrayPool<byte>.Shared.Rent(GetChunkByteCount(chunkInfo));
        try
        {
            // Rearrange chunk from pixel interleaved channels into scanline interleaved channels
            var sourceOffsets = GetInterleaveOffsets(channelOrder, out var bytesPerPixel, allChannelsRequired: true);

            int destOffset = 0;
            int scanlineCount = GetChunkScanLineCount(chunkInfo);
            for (int scanline = 0; scanline < scanlineCount; scanline++)
            {
                int channelIndex = 0;
                int scanlineOffset = offset + scanline * PixelsPerScanLine * bytesPerPixel;
                foreach (var channel in part.Channels)
                {
                    int channelBytesPerPixel = channel.Type.GetBytesPerPixel();
                    int sourceOffset = sourceOffsets[channelIndex++];

                    if (sourceOffset < 0)
                    {
                        // Skip channel
                        // TODO: Check that this is right...
                        continue;
                    }

                    sourceOffset += scanlineOffset;

                    for (int i = 0; i < PixelsPerScanLine; i++)
                    {
                        for (int j = 0; j < channelBytesPerPixel; j++)
                        {
                            pixelData[destOffset++] = data[sourceOffset + j];
                        }
                        sourceOffset += bytesPerPixel;
                    }
                }
            }

            return InternalWriteChunk(chunkInfo, pixelData, 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixelData);
        }
    }

    private int InternalWriteChunk(ChunkInfo chunkInfo, byte[] data, int dataIndex)
    {
        chunkInfo.FileOffset = writer.Position;

        long sizeOffset = WriteChunkHeader(chunkInfo);

        chunkInfo.PixelDataFileOffset = writer.Position;
        var dest = writer.GetStream();
        chunkInfo.UncompressedByteCount = GetChunkByteCount(chunkInfo);

        using (var source = new MemoryStream(data, dataIndex, chunkInfo.UncompressedByteCount))
        {
            var info = new PixelDataInfo(
                part.Channels,
                GetBounds(chunkInfo),
                chunkInfo.UncompressedByteCount
            );
            compressor.Compress(source, dest, info);
        }

        var size = (int)(writer.Position - sizeOffset - 4);
        writer.Seek(sizeOffset);
        writer.WriteInt(size);

        writer.Seek(offsetTableOffset + chunkInfo.Index * 8);
        writer.WriteULong((ulong)chunkInfo.FileOffset);

        writer.Seek(0, SeekOrigin.End);

        return chunkInfo.UncompressedByteCount;
    }

    private long WriteChunkHeader(ChunkInfo chunkInfo)
    {
        if (isMultiPart)
        {
            writer.WriteInt(chunkInfo.PartNumber);
        }

        if (IsTiled)
        {
            if (chunkInfo is not TileChunkInfo tileInfo)
            {
                throw new EXRFormatException($"Expected tile chunk info for {chunkInfo}");
            }
            writer.WriteInt(tileInfo.X);
            writer.WriteInt(tileInfo.Y);
            writer.WriteInt(tileInfo.LevelX);
            writer.WriteInt(tileInfo.LevelY);
        }
        else
        {
            if (chunkInfo is not ScanlineChunkInfo scanlineInfo)
            {
                throw new EXRFormatException($"Expected scanline chunk info for {chunkInfo}");
            }
            writer.WriteInt(scanlineInfo.Y);
        }
        long sizeOffset = writer.Position;
        writer.WriteInt(0); // Placeholder
        return sizeOffset;
    }

    private void CheckWriteCount(ChunkInfo chunkInfo, byte[] data, int index)
    {
        int count = data.Length - index;
        int expected = GetChunkByteCount(chunkInfo);
        if (count < expected)
        {
            throw new ArgumentException($"Expected chunk to write to be {expected} bytes, but got array (+ index) with {count} bytes", nameof(data));
        }
    }
}
