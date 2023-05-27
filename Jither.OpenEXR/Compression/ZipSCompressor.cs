using System.Buffers;
using System.IO.Compression;

namespace Jither.OpenEXR.Compression;

public class ZipSCompressor : Compressor
{
    public override int ScanLinesPerBlock => 1;

    public override CompressionResult InternalCompress(Stream source, Stream dest, PixelDataInfo info)
    {
        int length = (int)source.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            source.Read(buffer);
            ReorderAndPredict(buffer, length);
            using (var zlib = new ZLibStream(dest, CompressionLevel.Optimal, leaveOpen: true))
            {
                // TODO: Handle no-gain
                zlib.Write(buffer, 0, length);
                //zlib.Flush();
                return CompressionResult.Success;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override void InternalDecompress(Stream source, Stream dest, PixelDataInfo info)
    {
        int length = info.UncompressedByteSize;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            using (var zlib = new ZLibStream(source, CompressionMode.Decompress, leaveOpen: true))
            {
                // zlib.Read(buffer) isn't guaranteed to read the full stream.
                int totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    int bytesRead = zlib.Read(buffer, totalRead, buffer.Length - totalRead);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    totalRead += bytesRead;
                }
            }
            UnpredictAndReorder(buffer, length);
            dest.Write(buffer, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
