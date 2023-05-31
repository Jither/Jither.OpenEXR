using Jither.OpenEXR.Compression;
using Jither.OpenEXR.Converters;
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
            var chunkInfo = new ScanlineChunkInfo(part, chunkIndex, y);
            int bytesWritten = InternalWriteChunk(chunkInfo, data, sourceOffset);
            sourceOffset += bytesWritten;
        }
    }

    public void WriteChunk(ChunkInfo chunkInfo, byte[] data, int offset = 0)
    {
        CheckWriteCount(chunkInfo, data, offset);
        InternalWriteChunk(chunkInfo, data, offset);
    }

    public void WriteInterleaved(byte[] data, string[] channelOrder)
    {
        int sourceOffset = 0;
        var converter = new PixelInterleaveConverter(part.Channels, channelOrder);
        for (int chunkIndex = 0; chunkIndex < ChunkCount; chunkIndex++)
        {
            int y = chunkIndex * compressor.ScanLinesPerChunk + part.DataWindow.YMin;
            var chunkInfo = new ScanlineChunkInfo(part, chunkIndex, y);
            var pixelData = ArrayPool<byte>.Shared.Rent(chunkInfo.UncompressedByteCount);
            try
            {
                converter.ToEXR(chunkInfo.GetBounds(), data, pixelData, sourceOffset);
                int bytesWritten = InternalWriteChunk(chunkInfo, pixelData, 0);
                sourceOffset += bytesWritten;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pixelData);
            }
        }
    }

    public int WriteChunkInterleaved(ChunkInfo chunkInfo, Span<byte> data, string[] channelOrder, int sourceOffset = 0)
    {
        CheckWriteCount(chunkInfo, data, sourceOffset);
        CheckInterleavedPrerequisites();

        var converter = new PixelInterleaveConverter(part.Channels, channelOrder);
        var pixelData = ArrayPool<byte>.Shared.Rent(chunkInfo.UncompressedByteCount);
        try
        {
            converter.ToEXR(chunkInfo.GetBounds(), data, pixelData, sourceOffset);
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

        using (var source = new MemoryStream(data, dataIndex, chunkInfo.UncompressedByteCount))
        {
            var info = new PixelDataInfo(
                part.Channels,
                chunkInfo.GetBounds(),
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
        if (fileIsMultiPart)
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

    private static void CheckWriteCount(ChunkInfo chunkInfo, Span<byte> sourceData, int sourceIndex)
    {
        int actual = sourceData.Length - sourceIndex;
        int expected = chunkInfo.UncompressedByteCount;
        if (actual < expected)
        {
            throw new ArgumentException($"Expected chunk to write to be {expected} bytes, but got array (+ index) with {actual} bytes", nameof(sourceData));
        }
    }
}
