using Jither.OpenEXR.Drawing;
using System.Diagnostics.CodeAnalysis;

namespace Jither.OpenEXR.Attributes;

// Ignoring that this is a uint - it's inconsistent with everything else - e.g. DataWindow and DisplayWindow, which use signed integers
public record TileDesc(int XSize, int YSize, LevelMode LevelMode, RoundingMode RoundingMode)
{
    public byte Mode => (byte)(((int)RoundingMode << 4) | (int)LevelMode);

    public TileDesc(int xSize, int ySize, byte mode) : this(xSize, ySize, (LevelMode)(mode & 0xf), (RoundingMode)((mode & 0xf0) >> 4))
    {
    }

    public TileInformation GetTileInformation(int imageWidth, int imageHeight)
    {
        return new TileInformation(this, imageWidth, imageHeight);
    }
}

public class TileLevel
{
    public int Width { get; }
    public int Height { get; }

    public int LevelX { get; }
    public int LevelY { get; }

    public TileLevel(int levelX, int levelY, int width, int height)
    {
        LevelX = levelX;
        LevelY = levelY;
        Width = width;
        Height = height;
    }
}

public class TileInformation
{
    private readonly int imageWidth;
    private readonly int imageHeight;
    private readonly TileDesc tileDesc;
    public IReadOnlyList<TileLevel> Levels { get; }

    public TileInformation(TileDesc tileDesc, int imageWidth, int imageHeight)
    {
        this.tileDesc = tileDesc;
        this.imageWidth = imageWidth;
        this.imageHeight = imageHeight;
        Levels = CalculateLevels();
    }

    private int DivideWithRounding(int a, int b)
    {
        if (tileDesc.RoundingMode == RoundingMode.Down)
        {
            return a / b;
        }
        return a / b + (a % b > 0 ? 1 : 0);
    }

    private IReadOnlyList<TileLevel> CalculateLevels()
    {
        var levels = new List<TileLevel>();
        int width = imageWidth;
        int height = imageHeight;
        levels.Add(new TileLevel(0, 0, width, height));
        switch (tileDesc.LevelMode)
        {
            case LevelMode.MipMap:
                int index = 1;
                while (width > 1 || height > 1)
                {
                    if (width > 1)
                    {
                        width = DivideWithRounding(width, 2);
                    }
                    if (height > 1)
                    {
                        height = DivideWithRounding(height, 2);
                    }
                    levels.Add(new TileLevel(index, index, width, height));
                    index++;
                }
                break;
            case LevelMode.RipMap:
                int levelX = 1;
                int levelY = 0;
                while (height > 1)
                {
                    width = imageWidth;

                    height = DivideWithRounding(height, 2);
                    
                    while (width > 1)
                    {
                        width = DivideWithRounding(width, 2);
                        levels.Add(new TileLevel(levelX, levelY, width, height));
                        levelX++;
                    }
                    levelX = 0;
                    levelY++;
                }
                break;
            default:
            case LevelMode.One:
                break;
        }
        return levels;
    }
}