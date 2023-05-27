namespace Jither.OpenEXR.Compression;

public class ZipCompressor : ZipSCompressor
{
    public override int ScanLinesPerChunk { get; } = EXRCompression.ZIPS.GetScanLinesPerChunk();
}
