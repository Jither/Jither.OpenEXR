﻿namespace Jither.OpenEXR.Compression;

public class NullCompressor : Compressor
{
    public static NullCompressor Instance { get; } = new NullCompressor();

    public override int ScanLinesPerBlock => 1;

    public override CompressionResult InternalCompress(Stream source, Stream dest, PixelDataInfo info)
    {
        source.CopyTo(dest);
        return CompressionResult.Success;
    }

    public override void InternalDecompress(Stream source, Stream dest, PixelDataInfo info)
    {
        source.CopyTo(dest);
    }
}
