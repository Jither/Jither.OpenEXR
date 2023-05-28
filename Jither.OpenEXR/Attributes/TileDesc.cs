using Jither.OpenEXR.Drawing;
using System.Diagnostics.CodeAnalysis;

namespace Jither.OpenEXR.Attributes;

// Ignoring that this is a uint - it's inconsistent with everything else - e.g. DataWindow and DisplayWindow, which use signed integers
public record TileDesc(int XSize, int YSize, LevelMode LevelMode, RoundingMode RoundingMode)
{
    private IReadOnlyList<Dimensions<int>>? coverages;
    private IReadOnlyList<Dimensions<int>>? resolutions;

    // Number of levels in the x direction
    private int? xLevelCount;
    // Number of levels in the y direction
    private int? yLevelCount;

    public IReadOnlyList<Dimensions<int>> Coverages
    {
        get
        {
            if (coverages == null)
            {
                CalculateLevels();
            }
            return coverages;
        }
    }

    public IReadOnlyList<Dimensions<int>> Resolutions
    {
        get
        {
            if (resolutions == null)
            {
                CalculateLevels();
            }
            return resolutions;
        }
    }

    public byte Mode => (byte)(((int)RoundingMode << 4) | (int)LevelMode);

    public TileDesc(int xSize, int ySize, byte mode) : this(xSize, ySize, (LevelMode)(mode & 0xf), (RoundingMode)((mode & 0xf0) >> 4))
    {
    }

    public Dimensions<int> GetCoverage(int xLevel, int yLevel)
    {
        return Coverages[yLevel * xLevelCount!.Value + xLevel];
    }

    private int DivideWithRounding(int x, int y)
    {
        if (RoundingMode == RoundingMode.Down)
        {
            return x / y;
        }
        return x / y + (x % y != 0 ? 1 : 0);
    }

    [MemberNotNull(nameof(coverages), nameof(resolutions), nameof(xLevelCount), nameof(yLevelCount))]
    private void CalculateLevels()
    {
        var coverages = new List<Dimensions<int>>();
        var resolutions = new List<Dimensions<int>>();
        this.coverages = coverages;
        this.resolutions = resolutions;

        coverages.Add(new Dimensions<int>(XSize, YSize));
        resolutions.Add(new Dimensions<int>(XSize, YSize));
        
        int xSize, ySize, xCoverage, yCoverage;

        xLevelCount = 1;
        yLevelCount = 1;

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
                while (xSize > 1 || ySize > 1)
                {
                    xLevelCount++;
                    yLevelCount++;
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
                    resolutions.Add(new Dimensions<int>(xSize, ySize));
                    coverages.Add(new Dimensions<int>(xCoverage, yCoverage));
                }
                break;

            case LevelMode.RipMap:
                ySize = YSize;
                yCoverage = YSize;
                while (ySize > 1)
                {
                    yLevelCount++;

                    xSize = XSize;
                    xCoverage = XSize;

                    ySize = DivideWithRounding(ySize, 2);
                    yCoverage *= 2;

                    while (xSize > 1)
                    {
                        xSize = DivideWithRounding(xSize, 2);
                        xCoverage *= 2;
                        resolutions.Add(new Dimensions<int>(xSize, ySize));
                        coverages.Add(new Dimensions<int>(xCoverage, yCoverage));
                    }
                }

                xLevelCount = resolutions.Count / yLevelCount;
                break;
            default:
                throw new NotImplementedException($"{nameof(CalculateLevels)} not implemented for {LevelMode}");
        }
    }
}