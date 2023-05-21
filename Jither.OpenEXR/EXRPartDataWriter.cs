using System.Buffers;

namespace Jither.OpenEXR;

public class EXRPartDataWriter : EXRPartDataHandler
{
    private readonly EXRWriter writer;
    private readonly long offsetTableOffset;
    private readonly int chunkCount;
    private readonly bool isMultiPart;
    private int chunkIndex = 0;

    internal EXRPartDataWriter(EXRPart part, EXRVersion version, EXRWriter writer) : base(part)
    {
        this.writer = writer;
        this.isMultiPart = version.IsMultiPart;
        offsetTableOffset = writer.Position;
        chunkCount = (int)Math.Ceiling((double)part.DataWindow.Height / compressor.ScanLinesPerBlock);
    }

    public void WriteOffsetPlaceholders()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            writer.WriteULong(0xffffffffffffffffUL);
        }
    }

    public void WriteBlock(byte[] data, int index = 0)
    {
        CheckWriteCount(data, index);
        InternalWriteBlock(data, index);
    }

    public void Write(byte[] data)
    {
        int sourceIndex = 0;
        for (int i = 0; i < chunkCount; i++)
        {
            int bytesWritten = InternalWriteBlock(data, sourceIndex);
            sourceIndex += bytesWritten;
        }
    }

    public void WriteBlockInterleaved(byte[] data, IEnumerable<string> channelOrder, int index = 0)
    {
        CheckWriteCount(data, index);
        var pixelData = ArrayPool<byte>.Shared.Rent(BytesPerBlock);
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

            InternalWriteBlock(data, index);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixelData);
        }
    }

    private int InternalWriteBlock(byte[] data, int index)
    {
        // TODO: We're assuming INCREASING_Y when writing
        ulong blockOffset = (ulong)writer.Position;

        if (isMultiPart)
        {
            writer.WriteInt(chunkIndex);
        }
        writer.WriteInt(chunkIndex * compressor.ScanLinesPerBlock); // y
        long sizeOffset = writer.Position;
        writer.WriteInt(0); // Placeholder
        var dest = writer.GetStream();
        int bytesToWrite = chunkIndex < chunkCount - 1 ? BytesPerBlock : Math.Min(BytesPerBlock, BytesInLastBlock);
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
        chunkIndex++;

        return bytesToWrite;
    }

    private void CheckWriteCount(byte[] data, int index)
    {
        int count = data.Length - index;
        if (count < BytesPerBlock)
        {
            throw new ArgumentException($"Expected block to write to be {BytesPerBlock} bytes, but got array (+ index) with {count} bytes", nameof(data));
        }
    }
}
