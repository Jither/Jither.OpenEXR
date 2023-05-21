namespace Jither.OpenEXR.Compression;

public class PizCompressor : Compressor
{
    public override int ScanLinesPerBlock => 32;

    public override void Compress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }

    public override void Decompress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }
}
