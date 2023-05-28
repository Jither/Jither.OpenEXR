namespace Jither.OpenEXR;

public class TileChunkInfo : ChunkInfo
{
    public int X { get; }
    public int Y { get; }
    public int LevelX { get; }
    public int LevelY { get; }

    public TileChunkInfo(int index, int partNumber, int x, int y, int levelX, int levelY) : base(index, partNumber)
    {
        X = x;
        Y = y;
        LevelX = levelX;
        LevelY = levelY;
    }
}
