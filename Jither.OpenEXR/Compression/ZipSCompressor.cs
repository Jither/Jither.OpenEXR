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
            using (var intermediary = new MemoryStream())
            {
                using (var zlib = new ZLibStream(intermediary, CompressionLevel.Optimal, leaveOpen: true))
                {
                    zlib.Write(buffer, 0, length);
                }

                if (intermediary.Position >= info.UncompressedByteSize)
                {
                    return CompressionResult.NoGain;
                }
                intermediary.Position = 0;
                intermediary.CopyTo(dest);
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
                zlib.ReadExactly(buffer, 0, length);
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
