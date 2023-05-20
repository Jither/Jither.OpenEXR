using System.Buffers;

namespace Jither.OpenEXR;

public class EXRPartDataWriter : EXRPartDataHandler
{
    private readonly EXRWriter writer;
    private readonly long offsetTableOffset;
    private readonly int offsetTableSize;
    private int blockIndex = 0;

    internal EXRPartDataWriter(EXRPart part, EXRWriter writer) : base(part)
    {
        this.writer = writer;
        offsetTableOffset = writer.Position;
        offsetTableSize = (int)Math.Ceiling((double)part.DataWindow.Height / compressor.ScanLinesPerBlock);
    }

    public void WriteOffsetPlaceholders()
    {
        for (int i = 0; i < offsetTableSize; i++)
        {
            writer.WriteULong(0xffffffffffffffffUL);
        }
    }

    public void WriteBlock(byte[] data, int count = 0)
    {
        count = CheckWriteCount(data, count);
        InternalWriteBlock(writer, data, count);
    }

    public void WriteBlockInterleaved(byte[] data, IEnumerable<string> channelOrder, int count = 0)
    {
        count = CheckWriteCount(data, count);
        var pixelData = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            // Rearrange block from interleaved channels into consecutive channels
            var sourceOffsets = GetInterleaveOffsets(channelOrder, out var bytesPerPixel, allChannelsRequired: true);

            int channelIndex = 0;
            int destIndex = 0;
            foreach (var channel in part.Channels)
            {
                int channelBytesPerPixel = channel.Type.GetBytesPerPixel();
                int sourceIndex = sourceOffsets[channelIndex++];

                for (int i = 0; i < PixelsPerBlock; i++)
                {
                    for (int j = 0; j < channelBytesPerPixel; j++)
                    {
                        pixelData[destIndex++] = data[sourceIndex + j];
                    }
                    sourceIndex += channelBytesPerPixel;
                }
            }

            InternalWriteBlock(writer, data, count);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixelData);
        }
    }

    private void InternalWriteBlock(EXRWriter writer, byte[] data, int count)
    {
        // TODO: We're assuming INCREASING_Y when writing
        ulong blockOffset = (ulong)writer.Position;

        writer.WriteInt(blockIndex);
        writer.WriteInt(blockIndex * compressor.ScanLinesPerBlock); // y
        long sizeOffset = writer.Position;
        writer.WriteInt(0); // Placeholder
        var dest = writer.GetStream();
        using (var source = new MemoryStream(data, 0, count))
        {
            compressor.Compress(source, dest);
        }
        
        var size = (int)(writer.Position - sizeOffset);
        writer.Seek(sizeOffset);
        writer.WriteInt(size);

        writer.Seek(offsetTableOffset + blockIndex * 8);
        writer.WriteULong((ulong)blockOffset);

        writer.Seek(0, SeekOrigin.End);
        blockIndex++;
    }

    private int CheckWriteCount(byte[] data, int count)
    {
        if (count == 0)
        {
            count = data.Length;
        }
        if (count > data.Length)
        {
            throw new ArgumentException($"Specified number of bytes to write ({count}) exceeds the size of the data ({data.Length})");
        }
        if (count != BytesPerBlock)
        {
            throw new ArgumentException($"Expected block to write to be {BytesPerBlock} bytes, but got {count}", nameof(count));
        }
        return count;
    }
}
