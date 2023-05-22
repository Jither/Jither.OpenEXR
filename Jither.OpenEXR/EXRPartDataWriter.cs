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
        for (int i = 0; i < chunkCount; i++)
        {
            int bytesWritten = InternalWriteBlock(i, data, sourceOffset);
            sourceOffset += bytesWritten;
        }
    }

    public void WriteInterleaved(byte[] data, IEnumerable<string> channelOrder)
    {
        int sourceOffset = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            int bytesWritten = WriteBlockInterleaved(i, data, channelOrder, sourceOffset);
            sourceOffset += bytesWritten;
        }
    }

    public void WriteBlock(int chunkIndex, byte[] data, int offset = 0)
    {
        CheckWriteCount(chunkIndex, data, offset);
        InternalWriteBlock(chunkIndex, data, offset);
    }

    public int WriteBlockInterleaved(int chunkIndex, byte[] data, IEnumerable<string> channelOrder, int offset = 0)
    {
        CheckWriteCount(chunkIndex, data, offset);
        var pixelData = ArrayPool<byte>.Shared.Rent(GetBlockByteCount(chunkIndex));
        try
        {
            // Rearrange block from interleaved channels into consecutive channels
            var sourceOffsets = GetInterleaveOffsets(channelOrder, out var bytesPerPixel, allChannelsRequired: true);

            int destOffset = 0;
            int scanlineCount = GetBlockScanLineCount(chunkIndex);
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
                        sourceOffset += BytesPerPixel;
                    }
                }
            }

            return InternalWriteBlock(chunkIndex, pixelData, 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixelData);
        }
    }

    private int InternalWriteBlock(int chunkIndex, byte[] data, int index)
    {
        // TODO: We're assuming INCREASING_Y when writing
        ulong blockOffset = (ulong)writer.Position;

        if (isMultiPart)
        {
            writer.WriteInt(chunkIndex);
        }
        int y = chunkIndex * compressor.ScanLinesPerBlock + part.DataWindow.YMin;
        writer.WriteInt(y);
        long sizeOffset = writer.Position;
        writer.WriteInt(0); // Placeholder
        var dest = writer.GetStream();
        int bytesToWrite = GetBlockByteCount(chunkIndex);
        using (var source = new MemoryStream(data, index, bytesToWrite))
        {
            compressor.Compress(source, dest);
        }
        
        var size = (int)(writer.Position - sizeOffset - 4);
        writer.Seek(sizeOffset);
        writer.WriteInt(size);

        writer.Seek(offsetTableOffset + chunkIndex * 8);
        writer.WriteULong(blockOffset);

        writer.Seek(0, SeekOrigin.End);

        return bytesToWrite;
    }

    private void CheckWriteCount(int chunkIndex, byte[] data, int index)
    {
        int count = data.Length - index;
        int expected = GetBlockByteCount(chunkIndex);
        if (count < expected)
        {
            throw new ArgumentException($"Expected block to write to be {expected} bytes, but got array (+ index) with {count} bytes", nameof(data));
        }
    }
}
