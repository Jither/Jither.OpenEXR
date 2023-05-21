using Jither.OpenEXR.Attributes;
using Jither.OpenEXR.Compression;
using System.ComponentModel.DataAnnotations;

namespace Jither.OpenEXR;

public class EXRHeader
{
    public static readonly Chromaticities DefaultChromaticities = new(
        0.6400f, 0.3300f,
        0.3000f, 0.6000f,
        0.1500f, 0.0600f,
        0.3127f, 0.3290f
    );

    private readonly List<EXRAttribute> attributes = new();
    private readonly Dictionary<string, EXRAttribute> attributesByName = new();

    public IReadOnlyDictionary<string, EXRAttribute> AttributesByName => attributesByName;
    public IReadOnlyList<EXRAttribute> Attributes => attributes;

    public bool IsEmpty => attributes.Count == 0;

    public ChannelList Channels => GetAttributeOrThrow<ChannelList>("channels");

    public EXRCompression Compression
    {
        get => GetAttributeOrThrow<EXRCompression>("compression");
        set => SetAttribute(new EXRAttribute<EXRCompression>("compression", value));
    }

    public Box2i DataWindow
    {
        get => GetAttributeOrThrow<Box2i>("dataWindow");
        set => SetAttribute(new EXRAttribute<Box2i>("dataWindow", value));
    }

    public Box2i DisplayWindow
    {
        get => GetAttributeOrThrow<Box2i>("displayWindow");
        set => SetAttribute(new EXRAttribute<Box2i>("displayWindow", value));
    }

    public LineOrder LineOrder
    {
        get => GetAttributeOrThrow<LineOrder>("lineOrder");
        set => SetAttribute(new EXRAttribute<LineOrder>("lineOrder", value));
    }

    public float PixelAspectRatio
    {
        get => GetAttributeOrThrow<float>("pixelAspectRatio");
        set => SetAttribute(new EXRAttribute<float>("pixelAspectRatio", value));
    }

    public V2f ScreenWindowCenter
    {
        get => GetAttributeOrThrow<V2f>("screenWindowCenter");
        set => SetAttribute(new EXRAttribute<V2f>("screenWindowCenter", value));
    }

    public float ScreenWindowWidth
    {
        get => GetAttributeOrThrow<float>("screenWindowWidth");
        set => SetAttribute(new EXRAttribute<float>("screenWindowWidth", value));
    }

    /// <summary>
    /// Determines the size of the tiles and the number of resolution levels in the file.
    /// </summary>
    /// <remarks>
    /// The OpenEXR library ignores tile description attributes in scan line based files. The decision whether the file contains scan lines or tiles 
    /// is based on the value of bit 9 in the file’s version field, not on the presence of a tile description attribute.
    /// </remarks>
    public TileDesc? Tiles => GetAttributeOrDefault<TileDesc>("tiles");

    /// <summary>
    /// Specifies the view this part is associated with (mostly used for files which stereo views).
    /// 
    /// * A value of left indicate the part is associated with the left eye.
    /// * A value of right indicates the right eye
    /// 
    /// If there is no view attribute in the header, the entire part contains information not dependent on a particular eye.
    /// 
    /// This attribute can be used in the header for multi-part files.
    /// </summary>
    public string? View => GetAttributeOrDefault<string>("view");

    public string? Name
    {
        get => GetAttributeOrDefault<string>("name");
        set
        {
            if (value == null)
            {
                RemoveAttribute("name");
            }
            else
            {
                SetAttribute(new EXRAttribute<string>("name", value));
            }
        }
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
    public PartType? Type => GetAttributeOrDefault<PartType>("type");

    /// <summary>
    /// Version is required for deep data (deepscanline and deeptile) parts. If not specified for other parts, 1 is assumed.
    /// </summary>
    public int Version => GetAttributeOrDefault<int?>("version") ?? 1;

    /// <summary>
    /// Indicates the number of chunks in this part. Required if the multipart bit (12) is set.
    /// </summary>
    public int ChunkCount => GetAttributeOrDefault<int?>("chunkCount") ?? 1;

    /// <summary>
    /// For RGB images, specifies the CIE (x,y) chromaticities of the primaries and the white point.
    /// </summary>
    public Chromaticities Chromaticities => GetAttributeOrDefault<Chromaticities>("chromaticities") ?? DefaultChromaticities;

    public EXRHeader()
    {
    }

    public static EXRHeader ReadFrom(EXRReader reader, int maxNameLength)
    {
        var result = new EXRHeader();
        while (true)
        {
            var attribute = EXRAttribute.ReadFrom(reader, maxNameLength);
            if (attribute == null)
            {
                break;
            }
            result.SetAttribute(attribute);
        }
        return result;
    }

    public void WriteTo(EXRWriter writer)
    {
        foreach (var attribute in attributes)
        {
            attribute.WriteTo(writer);
        }
        writer.WriteByte(0);
    }

    /// <summary>
    /// Adds or replaces attribute. Only a single attribute with a given name may exist in the header.
    /// </summary>
    public void SetAttribute(EXRAttribute attribute)
    {
        if (attributesByName.TryGetValue(attribute.Name, out var existingAttribute))
        {
            attributes.Remove(existingAttribute);
        }
        attributes.Add(attribute);
        attributesByName[attribute.Name] = attribute;
    }

    public void RemoveAttribute(string name)
    {
        attributes.RemoveAll(a => a.Name == name);
        attributesByName.Remove(name);
    }

    public T GetAttributeOrThrow<T>(string name)
    {
        if (!TryGetAttribute<T>(name, out var attribute) || attribute == null)
        {
            throw new EXRFormatException($"Missing {name} attribute.");
        }
        return attribute;
    }

    private T? GetAttributeOrDefault<T>(string name)
    {
        if (!TryGetAttribute<T>(name, out var attribute) || attribute == null)
        {
            return default;
        }
        return attribute;
    }

    public bool TryGetAttribute<T>(string name, out T? result)
    {
        if (!attributesByName.TryGetValue(name, out var attr))
        {
            result = default;
            return false;
        }

        if (attr.UntypedValue == null)
        {
            result = default;
            return !typeof(T).IsClass && !typeof(T).IsInterface && !typeof(T).IsArray;
        }

        if (typeof(T).IsEnum)
        {
            if (Enum.TryParse(typeof(T), attr.UntypedValue.ToString(), true, out var enumResult))
            {
                result = (T)enumResult;
                return true;
            }
        }

        if (typeof(T).IsAssignableFrom(attr.UntypedValue.GetType()))
        {
            result = (T)attr.UntypedValue;
            return true;
        }
        result = default;
        return false;
    }
}
