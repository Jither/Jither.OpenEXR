namespace Jither.OpenEXR.Attributes;

public record Chromaticities(float RedX, float RedY, float GreenX, float GreenY, float BlueX, float BlueY, float WhiteX, float WhiteY);

public record TileDesc(uint XSize, uint YSize, LevelMode LevelMode, RoundingMode RoundingMode)
{
    public byte Mode => (byte)(((int)RoundingMode << 4) | (int)LevelMode);

    public TileDesc(uint xSize, uint ySize, byte mode) : this(xSize, ySize, (LevelMode)(mode & 0xf), (RoundingMode)((mode & 0xf0) >> 4))
    {
    }
}