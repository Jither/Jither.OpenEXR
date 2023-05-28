using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Jither.OpenEXR;

public class EXRFile : IDisposable
{
    private readonly List<EXRPart> parts = new();
    private readonly Dictionary<string, EXRPart> partsByName = new();
    private EXRReader? reader;
    private EXRWriter? writer;

    /// <summary>
    /// List of parts contained in the file, in the order their headers appear in the file.
    /// </summary>
    public IReadOnlyList<EXRPart> Parts => parts;

    /// <summary>
    /// List of part names in the order they appear in the file. Note that any unnamed single part will not appear in this list.
    /// </summary>
    public IEnumerable<string?> PartNames => parts.Select(p => p.Name);

    /// <summary>
    /// Parts contained in the file, indexed by name. Note that any unnamed single part will not appear here.
    /// </summary>
    public IReadOnlyDictionary<string, EXRPart> PartsByName => partsByName;

    /// <summary>
    /// Forces the OpenEXR version to 2 when writing this file, regardless of whether it uses version 2 features. True by default, since
    /// commonly used applications like DJV do not support version 1.
    /// </summary>
    public bool ForceVersion2 { get; set; } = true;

    /// <summary>
    /// File version information for files read from file or stream.
    /// This will be <c>null</c> for files that have not been read from an external source (i.e. files created from scratch).
    /// </summary>
    public EXRVersion? OriginalVersion { get; private set; }

    /// <summary>
    /// Creates a new, empty OpenEXR file.
    /// </summary>
    public EXRFile()
    {

    }

    /// <summary>
    /// Opens an existing OpenEXR file for reading. File and part headers will be read and available after construction.
    /// Use <see cref="EXRPart.DataReader"/> to read the pixel data of a part.
    /// </summary>
    public EXRFile(string path) : this(new FileStream(path, FileMode.Open, FileAccess.Read))
    {

    }

    /// <summary>
    /// Opens a OpenEXR file from a stream for reading. File and part headers will be read and available after construction.
    /// Use <see cref="EXRPart.DataReader"/> to read the pixel data of a part.
    /// </summary>
    public EXRFile(Stream stream) : this(new EXRReader(stream))
    {

    }

    private EXRFile(EXRReader reader)
    {
        this.reader = reader;
        ReadHeaders(reader);
    }

    /// <summary>
    /// Writes OpenEXR header and part headers to the given file path, overwriting any existing file at that path.
    /// Use <see cref="EXRPart.DataWriter"/> of the file's parts to write the pixel data.
    /// </summary>
    public void Write(string path)
    {
        Write(new FileStream(path, FileMode.Create, FileAccess.Write));
    }

    /// <summary>
    /// Writes OpenEXR header and part headers to a stream.
    /// Use <see cref="EXRPart.DataWriter"/> of the file's parts to write the pixel data.
    /// </summary>
    public void Write(Stream stream)
    {
        var version = DetermineVersionForWriting();
        writer = new EXRWriter(stream, version.MaxNameLength);
        WriteHeaders(writer, version);
    }

    /// <summary>
    /// Adds a part to the file.
    /// </summary>
    public void AddPart(EXRPart part)
    {
        if (part.Name != null)
        {
            if (partsByName.ContainsKey(part.Name))
            {
                throw new ArgumentException($"A part with the name '{part.Name}' already exists in this EXR file.");
            }
        }
        else
        {
            if (parts.Any(p => p.Name == null))
            {
                throw new ArgumentException($"A nameless part already exists in this EXR file.");
            }
        }
        parts.Add(part);
        part.AssignFile(this);
        if (part.Name != null)
        {
            partsByName.Add(part.Name, part);
        }
    }

    /// <summary>
    /// Removes any path with the given name from the file. Note that <c>null</c> may be passed to delete any unnamed single part.
    /// </summary>
    public void RemovePart(string name)
    {
        if (name == null)
        {
            parts.RemoveAll(p => p.Name == null);
        }
        else
        {
            parts.RemoveAll(p => p.Name == name);
            partsByName.Remove(name);
        }
    }

    internal int GetPartNumber(EXRPart part)
    {
        return parts.IndexOf(part);
    }

    [MemberNotNull(nameof(OriginalVersion))]
    private void ReadHeaders(EXRReader reader)
    {
        var magicNumber = reader.ReadInt();
        if (magicNumber != 20000630)
        {
            throw new EXRFormatException("Magic number not found.");
        }

        var version = OriginalVersion = EXRVersion.ReadFrom(reader);

        var headers = new List<EXRHeader>();

        if (version.IsMultiPart)
        {
            while (true)
            {
                var header = EXRHeader.ReadFrom(reader, version.MaxNameLength);
                if (header.IsEmpty)
                {
                    break;
                }
                headers.Add(header);
            }
        }
        else
        {
            var header = EXRHeader.ReadFrom(reader, version.MaxNameLength);
            headers.Add(header);
        }

        foreach (var header in headers)
        {
            var part = new EXRPart(header, version.IsSinglePartTiled);
            var dataReader = new EXRPartDataReader(part, version, reader);
            part.AssignDataReader(dataReader);
            AddPart(part);
        }
    }

    private void WriteHeaders(EXRWriter writer, EXRVersion version)
    {
        foreach (var part in parts)
        {
            part.PrepareForWriting(version.IsMultiPart, version.HasNonImageParts);
        }
        Validate(version);

        writer.WriteInt(20000630);

        version.WriteTo(writer);

        foreach (var part in parts)
        {
            part.WriteHeaderTo(writer);
        }
        
        if (version.IsMultiPart)
        {
            writer.WriteByte(0);
        }

        foreach (var part in parts)
        {
            var dataWriter = new EXRPartDataWriter(part, version, writer);
            dataWriter.WriteOffsetPlaceholders();
            part.AssignDataWriter(dataWriter);
        }
    }

    private EXRVersion DetermineVersionForWriting()
    {
        byte versionNumber = 1;
        EXRVersionFlags flags = EXRVersionFlags.None;
        Debug.Assert(parts.Count > 0);

        if (parts.Count > 1)
        {
            versionNumber = 2;
            flags |= EXRVersionFlags.MultiPart;
        }
        
        if (parts.Count == 1 && parts[0].Type == PartType.TiledImage)
        {
            flags |= EXRVersionFlags.IsSinglePartTiled;
        }

        if (parts.Any(p => p.Type == PartType.DeepScanLine || p.Type == PartType.DeepTiled))
        {
            versionNumber = 2;
            flags |= EXRVersionFlags.NonImageParts;
        }

        if (parts.Any(p => p.HasLongNames))
        {
            flags |= EXRVersionFlags.LongNames;
        }

        if (ForceVersion2)
        {
            versionNumber = 2;
        }

        return new EXRVersion(versionNumber, flags);
    }

    /// <summary>
    /// Does rudimentary validation of the setup of the file and its parts in preparation for writing.
    /// This method is called by the library before writing files, and will throw <see cref="EXRFormatException"/>
    /// in case of any issues.
    /// </summary>
    public void Validate(EXRVersion version)
    {
        if (parts.Count == 0)
        {
            throw new EXRFormatException($"File must have at least one part.");
        }

        if (parts.Count > 1 && parts.Any(p => p.Name == null))
        {
            throw new EXRFormatException($"All parts in multipart file must have a name.");
        }

        foreach (var part in parts)
        {
            part.Validate(version.IsMultiPart, version.HasNonImageParts);
        }
    }

    private bool disposed;

    /// <summary>
    /// Closes the file. No further reading or writing can occur. Alias of <seealso cref="Dispose"/>
    /// </summary>
    public void Close()
    {
        Dispose();
    }

    /// <summary>
    /// Closes the file. No further reading or writing can occur.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }
        if (disposing)
        {
            writer?.Dispose();
            writer = null;
            reader?.Dispose();
            reader = null;
        }
        disposed = true;
    }
}
