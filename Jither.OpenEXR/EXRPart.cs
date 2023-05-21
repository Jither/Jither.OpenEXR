using Jither.OpenEXR.Attributes;

namespace Jither.OpenEXR;

public class EXRPart
{
    private readonly EXRHeader header;

    public IReadOnlyList<EXRAttribute> Attributes => header.Attributes;

    /// <summary>
    /// Description of the image channels stored in the part.
    /// </summary>
    public ChannelList Channels
    {
        get => header.Channels;
        set => header.Channels = value;
    }

    /// <summary>
    /// Specifies the compression method applied to the pixel data of all channels in the part.
    /// </summary>
    public EXRCompression Compression
    {
        get => header.Compression;
        set => header.Compression = value;
    }

    /// <summary>
    /// The boundaries of an OpenEXR image are given as an axis-parallel rectangular region in pixel space, the display window.
    /// The display window is defined by the positions of the pixels in the upper left and lower right corners, (xMin, yMin) and (xMax, yMax).
    /// </summary>
    public Box2i DisplayWindow
    {
        get => header.DisplayWindow;
        set => header.DisplayWindow = value;
    }

    /// <summary>
    /// An OpenEXR file may not have pixel data for all the pixels in the display window, or the file may have pixel data beyond the boundaries of the display window.
    /// The region for which pixel data are available is defined by a second axis-parallel rectangle in pixel space, the data window.
    /// </summary>
    public Box2i DataWindow
    {
        get => header.DataWindow;
        set => header.DataWindow = value;
    }

    /// <summary>
    /// Specifies in what order the scan lines in the file are stored in the file (increasing Y, decreasing Y, or, for tiled images, also random Y).
    /// </summary>
    public LineOrder LineOrder
    {
        get => header.LineOrder;
        set => header.LineOrder = value;
    }

    /// <summary>
    /// Width divided by height of a pixel when the image is displayed with the correct aspect ratio.
    /// A pixel’s width (height) is the distance between the centers of two horizontally (vertically) adjacent pixels on the display.
    /// </summary>
    public float PixelAspectRatio
    {
        get => header.PixelAspectRatio;
        set => header.PixelAspectRatio = value;
    }

    /// <summary>
    /// With <seealso cref="ScreenWindowWidth"/> describes the perspective projection that produced the image. Programs that deal with images as purely
    /// two-dimensional objects may not be able so generate a description of a perspective projection. Those programs should set screenWindowWidth to 1,
    /// and screenWindowCenter to (0, 0).
    /// </summary>
    public V2f ScreenWindowCenter
    {
        get => header.ScreenWindowCenter;
        set => header.ScreenWindowCenter = value;
    }

    /// <summary>
    /// With <seealso cref="ScreenWindowCenter"/> describes the perspective projection that produced the image. Programs that deal with images as purely
    /// two-dimensional objects may not be able so generate a description of a perspective projection. Those programs should set screenWindowWidth to 1,
    /// and screenWindowCenter to (0, 0).
    /// </summary>
    public float ScreenWindowWidth
    {
        get => header.ScreenWindowWidth;
        set => header.ScreenWindowWidth = value;
    }

    /// <summary>
    /// The name attribute defines the name of each part. The name of each part must be unique. Names may contain ‘.’ characters to present a tree-like structure of the parts in a file.
    /// Required if the file is either MultiPart or NonImage.
    /// </summary>
    public string? Name => header.Name;

    public PartType? Type => header.Type;

    /// <summary>
    /// Indicates whether the part has R, G and B channels (of any type)
    /// </summary>
    public bool IsRGB => HasChannel("R") && HasChannel("G") && HasChannel("B");

    /// <summary>
    /// Indicates whether the part has an A channel (of any type)
    /// </summary>
    public bool HasAlpha => HasChannel("A");

    /// <summary>
    /// Indicates whether the part has a long (> 31 characters) name or any long attribute names or attribute types.
    /// </summary>
    public bool HasLongNames => Name?.Length > 31 || header.Attributes.Any(attr => attr.Name.Length > 31 || attr.Type.Length > 31);

    /// <summary>
    /// Provides access to reading the data from the part. Will be null until the file headers have been read.
    /// </summary>
    public EXRPartDataReader? DataReader { get; private set; }

    /// <summary>
    /// Provides access to writing data for a part. Will be null unless <see cref="EXRFile.Write"/> has been called and headers have been written.
    /// </summary>
    public EXRPartDataWriter? DataWriter { get; private set; }

    /// <summary>
    /// Creates a new part with default required attributes.
    /// </summary>
    public EXRPart(Box2i dataWindow, Box2i? displayWindow = null, string? name = null)
    {
        header = new EXRHeader();
        if (name != null)
        {
            header.Name = name;
        }
        // Set default required headers:
        header.DataWindow = dataWindow;
        header.DisplayWindow = displayWindow ?? header.DataWindow;
        header.Compression = EXRCompression.None;
        header.LineOrder = LineOrder.IncreasingY;
        header.PixelAspectRatio = 1;
        header.ScreenWindowCenter = new V2f(0, 0);
        header.ScreenWindowWidth = 1;
    }

    internal EXRPart(EXRHeader header)
    {
        this.header = header;
    }

    /// <summary>
    /// Returns the value of the attribute with the given name. If the attribute doesn't exist, returns the default value for the type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <returns></returns>
    public T? GetAttribute<T>(string name)
    {
        if (header.TryGetAttribute<T>(name, out var result))
        {
            return result;
        }
        return default;
    }

    /// <summary>
    /// Retrieves the value of the attribute with the given name, storing it into the value out parameter. The return value indicates whether the attribute existed.
    /// When the return value is false, value will be the default value for the type of attribute.
    /// </summary>
    public bool TryGetAttribute<T>(string name, out T? value)
    {
        return header.TryGetAttribute<T>(name, out value);
    }

    /// <summary>
    /// Retrieves the value of the attribute with the given name, and throws if the attribute wasn't found.
    /// </summary>
    public T GetAttributeOrThrow<T>(string name)
    {
        return header.GetAttributeOrThrow<T>(name);
    }

    /// <summary>
    /// Creates or updates an attribute with the given name, using the given value.
    /// </summary>
    public void SetAttribute<T>(string name, T value)
    {
        header.SetAttribute(new EXRAttribute<T>(name, value));
    }

    internal void WriteHeaderTo(EXRWriter writer)
    {
        header.WriteTo(writer);
    }

    internal void AssignDataReader(EXRPartDataReader reader)
    {
        this.DataReader = reader;
    }

    internal void AssignDataWriter(EXRPartDataWriter writer)
    {
        this.DataWriter = writer;
    }

    /// <summary>
    /// Checks whether a channel with the given name exists in the part.
    /// </summary>
    public bool HasChannel(string name)
    {
        return header.Channels.Any(c => c.Name == name);
    }
}
