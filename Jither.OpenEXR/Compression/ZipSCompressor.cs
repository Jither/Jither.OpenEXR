namespace Jither.OpenEXR.Compression;

public class ZipSCompressor : Compressor
{
    public override int ScanLinesPerBlock => 1;

    public override void Compress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }

    public override void Decompress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }
}
