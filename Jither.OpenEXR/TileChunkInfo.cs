using Jither.OpenEXR.Attributes;
using Jither.OpenEXR.Drawing;

namespace Jither.OpenEXR;

public class TileChunkInfo : ChunkInfo
{
    public int X { get; }
    public int Y { get; }
    public int LevelX { get; }
    public int LevelY { get; }

    protected TileDesc Tiles => part.Tiles ?? throw new InvalidOperationException($"Expected part to have a tiles attribute.");

    public TileChunkInfo(EXRPart part, int index, int x, int y, int levelX, int levelY) : base(part, index)
    {
        X = x * Tiles.XSize;
        Y = y * Tiles.YSize;
        LevelX = levelX;
        LevelY = levelY;
    }

    public override Bounds<int> GetBounds()
    {
        var dataWindow = part.DataWindow;
        int width = Math.Min(Tiles.XSize, dataWindow.XMax - X + 1);
        int height = Math.Min(Tiles.YSize, dataWindow.YMax - Y + 1);
        return new Bounds<int>(X, Y, width, height);
    }
}
