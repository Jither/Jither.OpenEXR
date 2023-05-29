using Jither.OpenEXR.Attributes;
using Jither.OpenEXR.Compression;

namespace Jither.OpenEXR;

public class EXRPart
{
    private readonly EXRHeader header;
    private readonly bool isSinglePartTiled;
    private EXRFile? file;

    public static readonly string[] RequiredAttributes = new[] {
        AttributeNames.Channels,
        AttributeNames.Compression,
        AttributeNames.DataWindow,
        AttributeNames.DisplayWindow,
        AttributeNames.LineOrder,
        AttributeNames.PixelAspectRatio,
        AttributeNames.ScreenWindowCenter,
        AttributeNames.ScreenWindowWidth
    };

    public static readonly string[] RequiredMultiPartAttributes = new[] {
        AttributeNames.Name,
        AttributeNames.Type,
        AttributeNames.ChunkCount,
    };

    public IReadOnlyList<EXRAttribute> Attributes => header.Attributes;

    /// <summary>
    /// The name attribute defines the name of each part. The name of each part must be unique. Names may contain ‘.’ characters to present a tree-like structure of the parts in a file.
    /// Required if the file is either MultiPart or NonImage.
    /// </summary>
    public string? Name
    {
        get => header.Name;
        set => header.Name = value;
    }

    /// <summary>
    /// Data types are defined by the type attribute. There are four types:
    /// 
    /// 1. Scan line images: indicated by a type attribute of scanlineimage.
    /// 2. Tiled images: indicated by a type attribute of tiledimage.
    /// 3. Deep scan line images: indicated by a type attribute of deepscanline.
    /// 4. Deep tiled images: indicated by a type attribute of deeptile.
    /// 
    /// Required if the file is either MultiPart or NonImage.
    /// </summary>
    /// <remarks>
    /// This value must agree with the version field’s tile bit (9) and non-image (deep data) bit (11) settings.
    /// </remarks>
    public PartType Type
    {
        get => header.Type switch
        {
            "scanlineimage" => PartType.ScanLineImage,
            "tiledimage" => PartType.TiledImage,
            "deepscanline" => PartType.DeepScanLine,
            "deeptile" => PartType.DeepTiled,
            _ => PartType.Unknown
        };
        set
        {
            header.Type = value switch
            {
                PartType.ScanLineImage => "scanlineimage",
                PartType.TiledImage => "tiledimage",
                PartType.DeepScanLine => "deepscanline",
                PartType.DeepTiled => "deeptile",
                _ => null,
            };
        }
    }

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
    /// Determines the size of the tiles and the number of resolution levels in the file. Null for non-tiled parts. See <see cref="IsTiled"/>
    /// </summary>
    /// <remarks>
    /// The OpenEXR library ignores tile description attributes in scan line based files. The decision whether the file contains scan lines or tiles 
    /// is based on the value of bit 9 in the file’s version field, not on the presence of a tile description attribute.
    /// </remarks>
    public TileDesc? Tiles
    {
        get => header.Tiles;
        set => header.Tiles = value;
    }

    /// <summary>
    /// Indicates whether the part is tiled - that is, whether it has a tiled type attribute or its in a file with a "single part tiled" version bit.
    /// </summary>
    public bool IsTiled => Type == PartType.TiledImage || Type == PartType.DeepTiled || isSinglePartTiled;

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
    /// Gets the part number of the part. -1 if the part isn't assigned to a file.
    /// </summary>
    public int PartNumber => file?.GetPartNumber(this) ?? -1;

    /// <summary>
    /// Creates a new part with default required attributes.
    /// </summary>
    public EXRPart(Box2i dataWindow, Box2i? displayWindow = null, string? name = null, PartType type = PartType.Unknown)
    {
        header = new EXRHeader();
        if (name != null)
        {
            Name = name;
        }
        Type = type;

        // Set default required headers:
        header.DataWindow = dataWindow;
        header.DisplayWindow = displayWindow ?? header.DataWindow;
        header.Compression = EXRCompression.None;
        header.LineOrder = LineOrder.IncreasingY;
        header.PixelAspectRatio = 1;
        header.ScreenWindowCenter = new V2f(0, 0);
        header.ScreenWindowWidth = 1;
    }

    internal EXRPart(EXRHeader header, bool isSinglePartTiled)
    {
        this.header = header;
        this.isSinglePartTiled = isSinglePartTiled;
    }

    internal void AssignFile(EXRFile file)
    {
        this.file = file;
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

    internal void PrepareForWriting(bool fileIsMultiPart)
    {
        if (fileIsMultiPart)
        {
            if (!header.HasAttribute(AttributeNames.ChunkCount))
            {
                int chunkCount = (int)Math.Ceiling((double)DataWindow.Height / Compression.GetScanLinesPerChunk());
                SetAttribute(AttributeNames.ChunkCount, chunkCount);
            }
        }
    }

    /// <summary>
    /// Does rudimentary validation of the part in preparation for writing.
    /// This method is called by the library before writing files, and will throw <see cref="EXRFormatException"/>
    /// in case of any issues.
    /// </summary>
    public void Validate(bool fileIsMultiPart, bool fileHasDeepData)
    {
        foreach (var requiredAttribute in RequiredAttributes)
        {
            if (!header.HasAttribute(requiredAttribute))
            {
                throw new EXRFormatException($"Part '{Name}' is missing required attribute '{requiredAttribute}'.");
            }
        }
        
        if (fileIsMultiPart || fileHasDeepData)
        {
            foreach (var requiredAttribute in RequiredMultiPartAttributes)
            {
                if (!header.HasAttribute(requiredAttribute))
                {
                    throw new EXRFormatException($"Part '{Name}' is missing '{requiredAttribute}' required for multi-part and deep data files.");
                }
            }
        }

        if (fileIsMultiPart)
        {
            if (!header.HasAttribute(AttributeNames.ChunkCount))
            {
                throw new EXRFormatException($"Part '{Name}' is missing '{AttributeNames.ChunkCount}' attribute required for multi-part files.");
            }
        }

        if (Type == PartType.TiledImage || Type == PartType.DeepTiled)
        {
            if (!header.HasAttribute(AttributeNames.Tiles))
            {
                throw new EXRFormatException($"Part '{Name}' is missing '{AttributeNames.Tiles}' attribute required for tiled parts.");
            }
        }

        if (Type == PartType.DeepScanLine || Type == PartType.DeepTiled)
        {
            if (!header.HasAttribute(AttributeNames.Version))
            {
                throw new EXRFormatException($"Part '{Name}' is missing '{AttributeNames.Version}' attribute required for deep data parts.");
            }
            if (!header.HasAttribute(AttributeNames.MaxSamplesPerPixel))
            {
                throw new EXRFormatException($"Part '{Name}' is missing '{AttributeNames.MaxSamplesPerPixel}' attribute required for deep data parts.");
            }
        }
    }

    /// <summary>
    /// Checks whether a channel with the given name exists in the part.
    /// </summary>
    public bool HasChannel(string name)
    {
        return header.Channels.Any(c => c.Name == name);
    }
}
