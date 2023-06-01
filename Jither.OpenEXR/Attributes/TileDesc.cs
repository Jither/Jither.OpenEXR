using Jither.OpenEXR.Drawing;
using Jither.OpenEXR.Helpers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Jither.OpenEXR.Attributes;

// Ignoring that this is a uint - it's inconsistent with everything else - e.g. DataWindow and DisplayWindow, which use signed integers
public record TileDesc(int XSize, int YSize, LevelMode LevelMode, RoundingMode RoundingMode)
{
    public byte Mode => (byte)(((int)RoundingMode << 4) | (int)LevelMode);

    public TileDesc(int xSize, int ySize, byte mode) : this(xSize, ySize, (LevelMode)(mode & 0xf), (RoundingMode)((mode & 0xf0) >> 4))
    {
    }

    public TilingInformation GetTilingInformation(int imageWidth, int imageHeight)
    {
        return new TilingInformation(this, imageWidth, imageHeight);
    }
}

public class TileLevel
{
    public int Width { get; }
    public int Height { get; }

    public int LevelX { get; }
    public int LevelY { get; }

    public int FirstChunkIndex { get; internal set; }
    public int ChunkCount { get; internal set; }

    public TileLevel(int levelX, int levelY, int width, int height)
    {
        LevelX = levelX;
        LevelY = levelY;
        Width = width;
        Height = height;
    }
}

public class TilingInformation
{
    private readonly int imageWidth;
    private readonly int imageHeight;
    private readonly TileDesc tileDesc;
    public IReadOnlyList<TileLevel> Levels { get; }

    public int LevelXCount { get; private set; }
    public int LevelYCount { get; private set; }


    public int TotalChunkCount { get; private set; }

    public TilingInformation(TileDesc tileDesc, int imageWidth, int imageHeight)
    {
        this.tileDesc = tileDesc;
        this.imageWidth = imageWidth;
        this.imageHeight = imageHeight;
        switch (tileDesc.LevelMode)
        {
            case LevelMode.One:
                LevelXCount = 1;
                LevelYCount = 1;
                break;
            case LevelMode.MipMap:
                LevelXCount = LevelYCount = RoundToInt(Math.Log2(Math.Max(imageWidth, imageHeight))) + 1;
                break;
            case LevelMode.RipMap:
                LevelXCount = RoundToInt(Math.Log2(imageWidth)) + 1;
                LevelYCount = RoundToInt(Math.Log2(imageHeight)) + 1;
                break;
            default:
                throw new NotSupportedException($"Unsupported level mode: {tileDesc.LevelMode}");
        }
        (Levels, TotalChunkCount) = CalculateLevels();
    }

    public TileLevel GetLevel(int levelX, int levelY)
    {
        switch (tileDesc.LevelMode)
        {
            case LevelMode.MipMap:
                if (levelX != levelY)
                {
                    throw new ArgumentException($"For mipmap parts, level number must be {nameof(levelX)} = {nameof(levelY)}");
                }
                if (levelX >= Levels.Count)
                {
                    throw new ArgumentOutOfRangeException($"This mipmap part has {Levels.Count} levels - level number must be between (0,0) and ({LevelXCount},{LevelYCount})");
                }
                return Levels[levelX];
            case LevelMode.One:
            case LevelMode.RipMap:
                int levelIndex = levelY * LevelXCount + levelX;
                if (levelIndex < 0 || levelIndex > Levels.Count)
                {
                    throw new ArgumentOutOfRangeException($"Level number for this part must be between (0,0) and ({LevelXCount},{LevelYCount})");
                }
                return Levels[levelX];
            default:
                throw new NotSupportedException($"Unsupported level mode: {tileDesc.LevelMode}");
        }
    }

    private int RoundToInt(double value)
    {
        return tileDesc.RoundingMode == RoundingMode.Down ? (int)value : (int)(Math.Ceiling(value));
    }

    private int DivideWithRounding(int a, int b)
    {
        if (tileDesc.RoundingMode == RoundingMode.Down)
        {
            return a / b;
        }
        return a / b + (a % b > 0 ? 1 : 0);
    }

    private (IReadOnlyList<TileLevel> levels, int totalChunkCount) CalculateLevels()
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

        int chunkIndex = 0;
        foreach (var level in levels)
        {
            level.FirstChunkIndex = chunkIndex;
            level.ChunkCount = MathHelpers.DivAndRoundUp(level.Width, tileDesc.XSize) * MathHelpers.DivAndRoundUp(level.Height, tileDesc.YSize);
            chunkIndex += level.ChunkCount;
        }

        return (levels, chunkIndex);
    }
}