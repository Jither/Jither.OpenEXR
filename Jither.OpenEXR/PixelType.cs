namespace Jither.OpenEXR;

public enum PixelType
{
    UInt = 0,
    Half = 1,
    Float = 2
}

public static class PixelTypeExtensions
{
    public static int GetBytesPerPixel(this PixelType pixelType)
    {
        return pixelType switch
        {
            PixelType.UInt => 4,
            PixelType.Half => 2,
            PixelType.Float => 4,
            _ => throw new NotImplementedException($"GetBytesPerPixel not implemented for {pixelType}")
        };
    }
}