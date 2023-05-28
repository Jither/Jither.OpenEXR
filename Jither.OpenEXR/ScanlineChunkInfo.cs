namespace Jither.OpenEXR;

public class ScanlineChunkInfo : ChunkInfo
{
    public int Y { get; }

    public ScanlineChunkInfo(int chunkIndex, int partNumber, int y) : base(chunkIndex, partNumber)
    {
        Y = y;
    }
}
