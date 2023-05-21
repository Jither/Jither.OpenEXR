using System.Buffers;

namespace Jither.OpenEXR.Compression;

public class RLECompressor : Compressor
{
    private const int MAX_RUN_LENGTH = 127;
    private const int MIN_RUN_LENGTH = 3;

    public override int ScanLinesPerBlock => 1;

    public override void Compress(Stream source, Stream dest)
    {
        int length = (int)source.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            source.Read(buffer);
            ReorderAndPredict(buffer, length);

            int runs = 0;
            int rune = 1;
            int bytesWritten = 0;
            while (runs < length)
            {
                byte runLength = 0;
                byte value = buffer[runs];
                while (rune < length && buffer[rune] == value && runLength < MAX_RUN_LENGTH)
                {
                    rune++;
                    runLength++;
                }

                if (runLength >= MIN_RUN_LENGTH - 1)
                {
                    dest.WriteByte(runLength);
                    dest.WriteByte(value);
                    bytesWritten += 2;
                    runs = rune;
                }
                else
                {
                    runLength++;
                    while (rune < length &&
                        ((
                            (rune + 1 >= length) ||
                            (buffer[rune] != buffer[rune + 1])
                        ) ||
                        (
                            (rune + 2 >= length) ||
                            (buffer[rune + 1] != buffer[rune + 2])
                        )) &&
                        runLength < MAX_RUN_LENGTH)
                    {
                        runLength++;
                        rune++;
                    }
                    dest.WriteByte(unchecked((byte)-runLength));
                    int count = rune - runs;
                    dest.Write(buffer, runs, count);
                    runs += count;
                    bytesWritten += count + 1;
                }
                rune++;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override void Decompress(Stream source, Stream dest)
    {
        int length = (int)dest.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            int bufferIndex = 0;
            while (true)
            {
                int b = source.ReadByte();
                if (b < 0)
                {
                    // End of stream
                    break;
                }
                int runCount = unchecked((sbyte)b);
                if (runCount < 0)
                {
                    runCount = -runCount;
                    source.ReadExactly(buffer, bufferIndex, runCount);
                    bufferIndex += runCount;
                }
                else
                {
                    var value = source.ReadByte();
                    if (value < 0)
                    {
                        throw new CompressionException($"Expected RLE value, but end of stream reached");
                    }
                    Array.Fill(buffer, (byte)value, bufferIndex, runCount + 1);
                    bufferIndex += runCount + 1;
                }
            }

            int outputLength = bufferIndex;
            UnpredictAndReorder(buffer, outputLength);

            dest.Write(buffer, 0, outputLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
