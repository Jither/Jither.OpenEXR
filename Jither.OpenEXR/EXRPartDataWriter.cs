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
        for (int i = 0; i < chunkCount; i++)
        {
            writer.WriteULong(0xffffffffffffffffUL);
        }
    }

    public void Write(byte[] data)
    {
        int sourceOffset = 0;
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int y = chunkIndex * compressor.ScanLinesPerChunk + part.DataWindow.YMin;
            int bytesWritten = InternalWriteChunk(chunkIndex, y, data, sourceOffset);
            sourceOffset += bytesWritten;
        }
    }

    public void WriteInterleaved(byte[] data, IEnumerable<string> channelOrder)
    {
        int sourceOffset = 0;
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            int y = chunkIndex * compressor.ScanLinesPerChunk + part.DataWindow.YMin;
            int bytesWritten = WriteChunkInterleaved(chunkIndex, y, data, channelOrder, sourceOffset);
            sourceOffset += bytesWritten;
        }
    }

    public void WriteChunk(int chunkIndex, int y, byte[] data, int offset = 0)
    {
        CheckWriteCount(chunkIndex, data, offset);
        InternalWriteChunk(chunkIndex, y, data, offset);
    }

    public int WriteChunkInterleaved(int chunkIndex, int y, byte[] data, IEnumerable<string> channelOrder, int offset = 0)
    {
        CheckWriteCount(chunkIndex, data, offset);
        CheckInterleavedPrerequisites();
        var pixelData = ArrayPool<byte>.Shared.Rent(GetChunkByteCount(chunkIndex));
        try
        {
            // Rearrange chunk from pixel interleaved channels into scanline interleaved channels
            var sourceOffsets = GetInterleaveOffsets(channelOrder, out var bytesPerPixel, allChannelsRequired: true);

            int destOffset = 0;
            int scanlineCount = GetChunkScanLineCount(chunkIndex);
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

            return InternalWriteChunk(chunkIndex, y, pixelData, 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixelData);
        }
    }

    private int InternalWriteChunk(int chunkIndex, int y, byte[] data, int index)
    {
        ulong chunkOffset = (ulong)writer.Position;

        if (isMultiPart)
        {
            writer.WriteInt(part.PartNumber);
        }
        writer.WriteInt(y);
        long sizeOffset = writer.Position;
        writer.WriteInt(0); // Placeholder
        var dest = writer.GetStream();
        int bytesToWrite = GetChunkByteCount(chunkIndex);
        using (var source = new MemoryStream(data, index, bytesToWrite))
        {
            var info = new PixelDataInfo(part.Channels, new System.Drawing.Rectangle(0, y, PixelsPerScanLine, GetChunkScanLineCount(chunkIndex)), bytesToWrite);
            compressor.Compress(source, dest, info);
        }
        
        var size = (int)(writer.Position - sizeOffset - 4);
        writer.Seek(sizeOffset);
        writer.WriteInt(size);

        writer.Seek(offsetTableOffset + chunkIndex * 8);
        writer.WriteULong(chunkOffset);

        writer.Seek(0, SeekOrigin.End);

        return bytesToWrite;
    }

    private void CheckWriteCount(int chunkIndex, byte[] data, int index)
    {
        int count = data.Length - index;
        int expected = GetChunkByteCount(chunkIndex);
        if (count < expected)
        {
            throw new ArgumentException($"Expected chunk to write to be {expected} bytes, but got array (+ index) with {count} bytes", nameof(data));
        }
    }
}
