namespace Jither.OpenEXR.Compression;

public class ZipCompressor : ZipSCompressor
{
    public override int ScanLinesPerBlock => 16;
}
