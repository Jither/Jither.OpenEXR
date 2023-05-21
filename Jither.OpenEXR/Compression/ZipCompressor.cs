namespace Jither.OpenEXR.Compression;

public class ZipCompressor : Compressor
{
    public override int ScanLinesPerBlock => 16;

    public override void Compress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }

    public override void Decompress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }
}
