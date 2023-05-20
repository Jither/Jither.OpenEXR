namespace Jither.OpenEXR;

[Flags]
public enum EXRImageSourceFormat
{
    Float = 1 << 1,
    Half = 1 << 2,
    UInt = 1 << 3,
    HasAlpha = 1 << 6,

    FloatRGBA = Float | HasAlpha,
    HalfRGBA = Half | HasAlpha,
    UIntRGBA = UInt | HasAlpha,
    FloatRGB = Float,
    HalfRGB = Half,
    UIntRGB = UInt
}

public static class EXRImageSourceFormatExtensions
{
    public static bool HasAlpha(this EXRImageSourceFormat format)
    {
        return format.HasFlag(EXRImageSourceFormat.HasAlpha);
    }

    public static PixelType GetPixelType(this EXRImageSourceFormat format)
    {
        if (format.HasFlag(EXRImageSourceFormat.Float))
        {
            return PixelType.Float;
        }
        if (format.HasFlag(EXRImageSourceFormat.Half))
        {
            return PixelType.Half;
        }
        if (format.HasFlag(EXRImageSourceFormat.UInt))
        {
            return PixelType.UInt;
        }

        throw new EXRFormatException($"Unknown source format: {format}");
    }
}