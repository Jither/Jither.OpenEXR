using Jither.OpenEXR.Drawing;

namespace Jither.OpenEXR.Attributes;

public record Box2i(int XMin, int YMin, int XMax, int YMax)
{
    public int Width => XMax - XMin + 1;
    public int Height => YMax - YMin + 1;

    public Bounds<int> ToBounds()
    {
        return new Bounds<int>(XMin, YMin, Width, Height);
    }
}
