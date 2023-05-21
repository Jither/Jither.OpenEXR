using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR.Compression;

public abstract class Compressor
{
    public abstract int ScanLinesPerBlock { get; }
    public abstract void Decompress(Stream source, Stream dest);
    public abstract void Compress(Stream source, Stream dest);

    protected static void UnpredictAndReorder(byte[] buffer, int length)
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

    protected static void ReorderAndPredict(byte[] buffer, int length)
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
