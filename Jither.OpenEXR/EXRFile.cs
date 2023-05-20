namespace Jither.OpenEXR;

public class EXRFile : IDisposable
{
    private readonly List<EXRPart> parts = new();
    private EXRReader? reader;

    public EXRVersion Version { get; private set; } = new EXRVersion(2, EXRVersionFlags.None);
    public IReadOnlyList<EXRPart> Parts => parts;
    public IEnumerable<string?> PartNames => parts.Select(p => p.Name);

    public EXRFile()
    {

    }

    private EXRFile(EXRReader reader)
    {
        this.reader = reader;
    }

    public static EXRFile FromFile(string path)
    {
        return FromStream(new FileStream(path, FileMode.Open, FileAccess.Read));
    }

    public static EXRFile FromStream(Stream stream)
    {
        var reader = new EXRReader(stream);
        var result = FromReader(reader);
        return result;
    }

    private static EXRFile FromReader(EXRReader reader)
    {
        var result = new EXRFile(reader);
        result.Read(reader);
        return result;
    }

    public void SaveAs(string path)
    {
        SaveAs(new FileStream(path, FileMode.Create, FileAccess.Write));
    }

    public void SaveAs(Stream stream)
    {
        using (var writer = new EXRWriter(stream, Version.MaxNameLength))
        {
            Write(writer);
        }
    }

    public EXRPart? GetPartByName(string name)
    {
        return Parts.SingleOrDefault(p => p.Name == name);
    }

    public void SaveRaw(Stream destination, IList<string>? channelOrder = null)
    {
        if (reader != null)
        {
            Parts[0].Decode(reader, destination, channelOrder);
        }
    }

    private void Read(EXRReader reader)
    {
        var magicNumber = reader.ReadInt();
        if (magicNumber != 20000630)
        {
            throw new EXRFormatException("Magic number not found.");
        }

        Version = EXRVersion.ReadFrom(reader);

        var headers = new List<EXRHeader>();

        if (Version.IsMultiPart)
        {
            while (true)
            {
                var header = EXRHeader.ReadFrom(reader, Version.MaxNameLength);
                if (header.IsEmpty)
                {
                    break;
                }
                headers.Add(header);
            }
        }
        else
        {
            var header = EXRHeader.ReadFrom(reader, Version.MaxNameLength);
            headers.Add(header);
        }

        for (int i = 0; i < headers.Count; i++)
        {
            parts.Add(new EXRPart(reader, Version, headers[i]));
        }
    }


    private void Write(EXRWriter writer)
    {
        writer.WriteInt(20000630);

        Version.WriteTo(writer);

        foreach (var part in parts)
        {
            part.Header.WriteTo(writer);
        }
        
        if (Version.IsMultiPart)
        {
            writer.WriteByte(0);
        }

        foreach (var part in parts)
        {
            part.WriteOffsetsTo(writer);
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
            reader?.Dispose();
            reader = null;
        }
        disposed = true;
    }
}
