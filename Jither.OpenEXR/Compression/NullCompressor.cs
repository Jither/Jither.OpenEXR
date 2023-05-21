namespace Jither.OpenEXR.Compression;

public class NullCompressor : Compressor
{
    public override int ScanLinesPerBlock => 1;

    public override void Compress(Stream source, Stream dest)
    {
        source.CopyTo(dest);
    }

    public override void Decompress(Stream source, Stream dest)
    {
        source.CopyTo(dest);
    }
}
