using Jither.OpenEXR.Drawing;

namespace Jither.OpenEXR.Converters;

/// <summary>
/// PixelConverters handle conversion from/to OpenEXR's pixel data structure.
/// </summary>
internal abstract class PixelConverter
{
    public abstract void ToEXR(Bounds<int> bounds, Span<byte> source, Span<byte> dest, int sourceStartOffset);
    public abstract void FromEXR(Bounds<int> bounds, Span<byte> source, Span<byte> dest, int sourceStartOffset);
}
