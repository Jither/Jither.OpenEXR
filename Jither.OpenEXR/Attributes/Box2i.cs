namespace Jither.OpenEXR.Attributes;

public record Box2i(int XMin, int YMin, int XMax, int YMax)
{
    public int Width => XMax - XMin + 1;
    public int Height => YMax - YMin + 1;
}
