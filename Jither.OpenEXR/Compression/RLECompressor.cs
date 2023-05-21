using System.Buffers;

namespace Jither.OpenEXR.Compression;

public class RLECompressor : Compressor
{
    private const int MAX_RUN_LENGTH = 127;
    private const int MIN_RUN_LENGTH = 3;

    public override int ScanLinesPerBlock => 1;

    public override void Compress(Stream source, Stream dest)
    {
        int end = (int)source.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(end);
        try
        {
            source.Read(buffer);
            ReorderAndPredict(buffer, end);

            int runs = 0;
            int rune = 1;
            int bytesWritten = 0;
            while (runs < end)
            {
                byte runLength = 0;
                byte value = buffer[runs];
                while (rune < end && buffer[rune] == value && runLength < MAX_RUN_LENGTH)
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
                    while (rune < end &&
                        ((
                            (rune + 1 >= end) ||
                            (buffer[rune] != buffer[rune + 1])
                        ) ||
                        (
                            (rune + 2 >= end) ||
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
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)dest.Length);
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

    private static void UnpredictAndReorder(byte[] buffer, int length)
    {
        byte[] temp = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            // Convert deltas to actual values
            int t1 = 1;
            temp[0] = buffer[0];
            while (t1 < length)
            {
                int d = (sbyte)buffer[t1 - 1] + (sbyte)buffer[t1] - 128;
                temp[t1] = unchecked((byte)d);
                t1++;
            }

            // Data is split into two parts containing the bytes at odd and even offsets, respectively. Reorder them:
            t1 = 0;
            int t2 = (length + 1) / 2;
            int s = 0;
            while (s < length)
            {
                buffer[s++] = temp[t1++];
                if (s < length)
                {
                    buffer[s++] = temp[t2++];
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(temp);
        }
    }

    private static void ReorderAndPredict(byte[] buffer, int length)
    {
        byte[] temp = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            // Split data into two parts containing the bytes at odd and even offsets, respectively.
            int t1 = 0;
            int t2 = (length + 1) / 2;
            int s = 0;

            while (s < length)
            {
                temp[t1++] = buffer[s++];
                if (s < length)
                {
                    temp[t2++] = buffer[s++];
                }
            }

            // Convert values to deltas
            t1 = 1;
            byte previous = temp[0];
            buffer[0] = previous;
            while (t1 < length)
            {
                byte current = temp[t1];
                int d = (sbyte)(current - previous + 128 + 256);
                previous = current;
                buffer[t1++] = unchecked((byte)d);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(temp);
        }
    }
}
