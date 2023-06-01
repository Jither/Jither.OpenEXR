using Jither.OpenEXR.Drawing;
using System.Diagnostics.CodeAnalysis;

namespace Jither.OpenEXR.Attributes;

public record TileLevel(int X, int Y, Dimensions<int> Coverage, Dimensions<int> Resolution)
{
}

// Ignoring that this is a uint - it's inconsistent with everything else - e.g. DataWindow and DisplayWindow, which use signed integers
public record TileDesc(int XSize, int YSize, LevelMode LevelMode, RoundingMode RoundingMode)
{
    private IReadOnlyList<TileLevel>? levels;

    public int LevelCountX => CalculateLevelCount(XSize);
    public int LevelCountY => CalculateLevelCount(YSize);

    public IReadOnlyList<TileLevel>? Levels
    {
        get
        {
            if (levels == null)
            {
                CalculateLevels();
            }
            return levels;
        }
    }

    public TileLevel GetLevel(int x, int y)
    {
        if (levels == null)
        {
            CalculateLevels();
        }
        if (x < 0 || x >= LevelCountX)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"This part contains {LevelCountX} horizontal levels. x must be between 0 and {LevelCountX - 1}.");
        }
        if (y < 0 || y >= LevelCountX)
        {
            throw new ArgumentOutOfRangeException(nameof(y), $"This part contains {LevelCountY} vertical levels. y must be between 0 and {LevelCountY - 1}.");
        }
        if (LevelMode == LevelMode.MipMap)
        {
            if (x != y)
            {
                throw new ArgumentException("Level number for mipmap part must be x = y.");
            }
            // Mipmap levels are stored as a "1D array".
            return levels[x];
        }

        // Rip maps are stored as "2D". This also works for "one level", where y and x are both 0.
        return levels[y * LevelCountX + x];
    }

    public byte Mode => (byte)(((int)RoundingMode << 4) | (int)LevelMode);

    public TileDesc(int xSize, int ySize, byte mode) : this(xSize, ySize, (LevelMode)(mode & 0xf), (RoundingMode)((mode & 0xf0) >> 4))
    {
    }

    private int DivideWithRounding(int x, int y)
    {
        if (RoundingMode == RoundingMode.Down)
        {
            return x / y;
        }
        return x / y + (x % y != 0 ? 1 : 0);
    }

    private int CalculateLevelCount(int dimension)
    {
        switch (LevelMode)
        {
            case LevelMode.MipMap:
                double result = Math.Log2(Math.Max(XSize, YSize));
                return (int)(RoundingMode == RoundingMode.Down ? Math.Floor(result) : Math.Ceiling(result)) + 1;
            case LevelMode.RipMap:
                result = Math.Log2(dimension);
                return (int)(RoundingMode == RoundingMode.Down ? Math.Floor(result) : Math.Ceiling(result)) + 1;
            default:
            case LevelMode.One:
                return 1;
        }
    }

    [MemberNotNull(nameof(levels))]
    private void CalculateLevels()
    {
        var levels = new List<TileLevel>();
        this.levels = levels;

        levels.Add(new TileLevel(0, 0, new Dimensions<int>(XSize, YSize), new Dimensions<int>(XSize, YSize)));
        
        int xSize, ySize, xCoverage, yCoverage;

        // "In a file with multiple levels, tiles have the same size, regardless of their level. Lower-resolution levels contain fewer, rather than smaller, tiles."
        // Here, we calculate the number of pixels each tile level covers, as well as the tile resolution for each tile level.

        switch (LevelMode)
        {
            case LevelMode.One:
                break;

            case LevelMode.MipMap:
                xSize = XSize;
                ySize = YSize;
                xCoverage = XSize;
                yCoverage = YSize;
                int index = 1;
                while (xSize > 1 || ySize > 1)
                {
                    if (xSize > 1)
                    {
                        xSize = DivideWithRounding(xSize, 2);
                    }
                    if (ySize > 1)
                    {
                        ySize = DivideWithRounding(ySize, 2);
                    }
                    xCoverage *= 2;
                    yCoverage *= 2;

                    levels.Add(new TileLevel(index, index, new Dimensions<int>(xCoverage, yCoverage), new Dimensions<int>(xSize, ySize)));
                    index++;
                }
                break;

            case LevelMode.RipMap:
                ySize = YSize;
                yCoverage = YSize;
                int yIndex = 0;
                int xIndex = 1;
                while (ySize > 1)
                {
                    xSize = XSize;
                    xCoverage = XSize;

                    ySize = DivideWithRounding(ySize, 2);
                    yCoverage *= 2;

                    while (xSize > 1)
                    {
                        xSize = DivideWithRounding(xSize, 2);
                        xCoverage *= 2;
                        levels.Add(new TileLevel(xIndex, yIndex, new Dimensions<int>(xCoverage, yCoverage), new Dimensions<int>(xSize, ySize)));
                        xIndex++;
                    }
                    xIndex = 0;
                    yIndex++;
                }

                break;
            default:
                throw new NotImplementedException($"{nameof(CalculateLevels)} not implemented for {LevelMode}");
        }
    }
}