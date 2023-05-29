namespace Jither.OpenEXR;

public abstract class ChunkInfo
{
    public int Index { get; }
    // OpenEXR uses ulong, but .NET doesn't, and this ought to be enough...
    public long FileOffset { get; set; }
    public long PixelDataFileOffset { get; set; }
    public int CompressedByteCount { get; set; }
    public int UncompressedByteCount { get; set; }
    public int PartNumber { get; }

    protected ChunkInfo(int index, int partNumber)
    {
        Index = index;
        PartNumber = partNumber;
    }

    public override string ToString()
    {
        return $"chunk part {PartNumber} index {Index}";
    }
}
