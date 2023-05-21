using System.Diagnostics;

namespace Jither.OpenEXR;

public class EXRFile : IDisposable
{
    private readonly List<EXRPart> parts = new();
    private readonly Dictionary<string, EXRPart> partsByName = new();
    private EXRReader? reader;
    private EXRWriter? writer;

    public IReadOnlyDictionary<string, EXRPart> PartsByName => partsByName;
    public IReadOnlyList<EXRPart> Parts => parts;

    public IEnumerable<string?> PartNames => parts.Select(p => p.Name);

    public EXRPartDataReaderList DataReaders { get; } = new();
    public EXRPartDataWriterList DataWriters { get; } = new();

    /// <summary>
    /// Forces the OpenEXR version to 2 when writing this file, regardless of whether it uses version 2 features.
    /// </summary>
    public bool ForceVersion2 { get; set; }

    public EXRFile()
    {

    }

    public EXRFile(string path) : this(new FileStream(path, FileMode.Open, FileAccess.Read))
    {

    }

    public EXRFile(Stream stream) : this(new EXRReader(stream))
    {

    }

    private EXRFile(EXRReader reader)
    {
        this.reader = reader;
        ReadHeaders(reader);
    }

    public void Write(string path)
    {
        Write(new FileStream(path, FileMode.Create, FileAccess.Write));
    }

    public void Write(Stream stream)
    {
        var version = DetermineVersion();
        writer = new EXRWriter(stream, version.MaxNameLength);
        WriteHeaders(writer, version);
    }

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
        if (part.Name != null)
        {
            partsByName.Add(part.Name, part);
        }
    }

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

    private void ReadHeaders(EXRReader reader)
    {
        var magicNumber = reader.ReadInt();
        if (magicNumber != 20000630)
        {
            throw new EXRFormatException("Magic number not found.");
        }

        var version = EXRVersion.ReadFrom(reader);

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
            var part = new EXRPart(header);
            var dataReader = new EXRPartDataReader(part, version, reader);
            DataReaders.Add(part.Name, dataReader);
            AddPart(part);
        }
    }

    private void WriteHeaders(EXRWriter writer, EXRVersion version)
    {
        Validate();

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
            DataWriters.Add(part.Name, dataWriter);
        }
    }

    private EXRVersion DetermineVersion()
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

    public void Validate()
    {
        if (parts.Count == 0)
        {
            throw new EXRFormatException($"File must have at least one part.");
        }

        if (parts.Count > 1 && parts.Any(p => p.Name == null))
        {
            throw new EXRFormatException($"All parts in multipart file must have a name.");
        }
    }

    private bool disposed;

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
