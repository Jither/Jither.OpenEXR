using Jither.OpenEXR.Attributes;
using System;
using System.Reflection.PortableExecutable;

namespace Jither.OpenEXR;

public class EXRPart
{
    private readonly EXRHeader header;

    public ChannelList Channels => header.Channels;
    public EXRCompression Compression => header.Compression;
    public Box2i DataWindow => header.DataWindow;
    public Box2i DisplayWindow => header.DisplayWindow;
    public LineOrder LineOrder => header.LineOrder;
    public float PixelAspectRatio => header.PixelAspectRatio;
    public V2f ScreenWindowCenter => header.ScreenWindowCenter;
    public float ScreenWindowWidth => header.ScreenWindowWidth;

    public string? Name => header.Name;
    public PartType? Type => header.Type;

    public bool IsRGB => HasChannel("R") && HasChannel("G") && HasChannel("B");
    public bool HasAlpha => HasChannel("A");

    public bool HasLongNames => Name?.Length > 31 || header.Attributes.Any(attr => attr.Name.Length > 31 || attr.Type.Length > 31);

    public EXRPartDataReader? DataReader { get; set; }
    public EXRPartDataWriter? DataWriter { get; set; }

    public EXRPart(EXRHeader header)
    {
        this.header = header;
    }

    public T? GetAttribute<T>(string name)
    {
        if (header.TryGetAttribute<T>(name, out var result))
        {
            return result;
        }
        return default;
    }

    public T GetAttributeOrThrow<T>(string name)
    {
        return header.GetAttributeOrThrow<T>(name);
    }

    public void SetAttribute<T>(string name, T value)
    {
        header.SetAttribute(new EXRAttribute<T>(name, value));
    }

    public void WriteHeaderTo(EXRWriter writer)
    {
        header.WriteTo(writer);
    }

    public bool HasChannel(string name)
    {
        return header.Channels.Any(c => c.Name == name);
    }
}
