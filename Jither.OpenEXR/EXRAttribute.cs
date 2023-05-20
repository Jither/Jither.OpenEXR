using Jither.OpenEXR.Attributes;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Jither.OpenEXR;

public class EXRAttribute
{
    public string Name { get; private set; }
    public string Type { get; private set; }
    public int Size { get; private set; }
    public object Value { get; private set; }

    private EXRAttribute(string name, string type, int size, object value)
    {
        Name = name;
        Type = type;
        Size = size;
        Value = value;
    }

    public static EXRAttribute? ReadFrom(EXRReader reader, int maxNameLength)
    {
        string name, type;
        int size;
        object value;

        try
        {
            name = reader.ReadStringZ(maxNameLength);
        }
        catch (Exception ex)
        {
            throw new EXRFormatException("Error reading header attribute name.", ex);
        }

        if (name == String.Empty)
        {
            return null;
        }

        try
        {
            type = reader.ReadStringZ(maxNameLength);
        }
        catch (Exception ex)
        {
            throw new EXRFormatException("Error reading header attribute type.", ex);
        }

        if (type == String.Empty)
        {
            throw new EXRFormatException("Empty header attribute type.");
        }

        size = reader.ReadInt();
        void CheckSize(int expectedSize)
        {
            if (size != expectedSize)
            {
                throw new EXRFormatException($"Expected size of header attribute {name} (type: {type}) to be {expectedSize} bytes, but was {size}.");
            }
        }
        switch (type)
        {
            case "box2i":
                CheckSize(16);
                value = new Box2i(reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt());
                break;
            case "box2f":
                CheckSize(16);
                value = new Box2f(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                break;
            case "chromaticities":
                CheckSize(32);
                value = new Chromaticities(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                break;
            case "compression":
                CheckSize(1);
                value = (EXRCompression)reader.ReadByte();
                break;
            case "double":
                CheckSize(4);
                value = reader.ReadDouble();
                break;
            case "envmap":
                CheckSize(1);
                value = (EnvironmentMap)reader.ReadByte();
                break;
            case "float":
                CheckSize(4);
                value = reader.ReadFloat();
                break;
            case "int":
                CheckSize(4);
                value = reader.ReadInt();
                break;
            case "keycode":
                CheckSize(28);
                value = new KeyCode(reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt(), reader.ReadInt());
                break;
            case "lineOrder":
                CheckSize(1);
                value = (LineOrder)reader.ReadByte();
                break;
            case "m33f":
                CheckSize(36);
                value = new M33f(reader.ReadFloatArray(9));
                break;
            case "m44f":
                CheckSize(64);
                value = new M44f(reader.ReadFloatArray(16));
                break;
            case "rational":
                CheckSize(8);
                value = new Attributes.Rational(reader.ReadInt(), reader.ReadUInt());
                break;
            case "string":
                value = reader.ReadString(size);
                break;
            case "stringvector":
                var list = new List<string>();
                if (size != 0 && size < 4)
                {
                    throw new EXRFormatException($"Expected size of 0 or 4+ for size of attribute {name} (type: {type}), but was: {size}");
                }
                value = list;
                long totalRead = 0;

                while (totalRead < size)
                {
                    var start = reader.Position;
                    var str = reader.ReadString();
                    list.Add(str);
                    totalRead += reader.Position - start;
                }
                if (totalRead != size)
                {
                    throw new EXRFormatException($"Attribute {name} (type: {type}) declared size to be {size} bytes, but read {totalRead}");
                }
                break;
            case "tiledesc":
                CheckSize(9);
                value = new TileDesc(reader.ReadUInt(), reader.ReadUInt(), reader.ReadByte());
                break;
            case "timecode":
                CheckSize(8);
                value = new TimeCode(reader.ReadUInt(), reader.ReadUInt());
                break;
            case "v2i":
                CheckSize(8);
                value = new V2i(reader.ReadInt(), reader.ReadInt());
                break;
            case "v2f":
                CheckSize(8);
                value = new V2f(reader.ReadFloat(), reader.ReadFloat());
                break;
            case "v3i":
                CheckSize(12);
                value = new V3i(reader.ReadInt(), reader.ReadInt(), reader.ReadInt());
                break;
            case "v3f":
                CheckSize(12);
                value = new V3f(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
                break;
            case "chlist":
                try
                {
                    var channelList = ChannelList.ReadFrom(reader, size);
                    value = channelList;
                }
                catch (Exception ex)
                {
                    throw new EXRFormatException($"Failed reading channel list '{name}'.", ex);
                }
                break;
            case "preview":
            default:
                value = reader.ReadBytes(size);
                break;
        }

        return new EXRAttribute(name, type, size, value);
    }

    public void WriteTo(EXRWriter writer)
    {
        if (Name == null)
        {
            return;
        }
        writer.WriteStringZ(Name);

        writer.WriteStringZ(Type);

        void WriteSize(int size)
        {
            writer.WriteInt(size);
        }

        switch (Value)
        {
            case Box2i box2i:
                WriteSize(16);
                writer.WriteInt(box2i.XMin);
                writer.WriteInt(box2i.YMin);
                writer.WriteInt(box2i.XMax);
                writer.WriteInt(box2i.YMax);
                break;
            case Box2f box2f:
                WriteSize(16);
                writer.WriteFloat(box2f.XMin);
                writer.WriteFloat(box2f.YMin);
                writer.WriteFloat(box2f.XMax);
                writer.WriteFloat(box2f.YMax);
                break;
            case Chromaticities chromaticities:
                WriteSize(32);
                writer.WriteFloat(chromaticities.RedX);
                writer.WriteFloat(chromaticities.RedY);
                writer.WriteFloat(chromaticities.GreenX);
                writer.WriteFloat(chromaticities.GreenY);
                writer.WriteFloat(chromaticities.BlueX);
                writer.WriteFloat(chromaticities.BlueY);
                writer.WriteFloat(chromaticities.WhiteX);
                writer.WriteFloat(chromaticities.WhiteY);
                break;
            case EXRCompression compression:
                WriteSize(1);
                writer.WriteByte((byte)compression);
                break;
            case double d:
                WriteSize(4);
                writer.WriteDouble(d);
                break;
            case EnvironmentMap envMap:
                WriteSize(1);
                writer.WriteByte((byte)envMap);
                break;
            case float f:
                WriteSize(4);
                writer.WriteFloat(f);
                break;
            case int i:
                WriteSize(4);
                writer.WriteInt(i);
                break;
            case KeyCode keyCode:
                WriteSize(28);
                writer.WriteInt(keyCode.FilmMFCCode);
                writer.WriteInt(keyCode.FilmType);
                writer.WriteInt(keyCode.Prefix);
                writer.WriteInt(keyCode.Count);
                writer.WriteInt(keyCode.PerfOffset);
                writer.WriteInt(keyCode.PerfsPerFrame);
                writer.WriteInt(keyCode.PerfsPerCount);
                break;
            case LineOrder lineOrder:
                WriteSize(1);
                writer.WriteByte((byte)lineOrder);
                break;
            case M33f m33f:
                WriteSize(36);
                writer.WriteFloatArray(m33f.Values);
                break;
            case M44f m44f:
                WriteSize(64);
                writer.WriteFloatArray(m44f.Values);
                break;
            case Attributes.Rational rational:
                WriteSize(8);
                writer.WriteInt(rational.Numerator);
                writer.WriteUInt(rational.Denominator);
                break;
            case string str:
                writer.WriteString(str);
                break;
            case List<string> stringVector:
                var sizePosition = writer.Position;
                writer.WriteInt(0); // Placeholder
                foreach (var str in stringVector)
                {
                    writer.WriteString(str);
                }
                var endPosition = writer.Position;
                writer.Seek((ulong)sizePosition, SeekOrigin.Begin);
                WriteSize((int)(endPosition - sizePosition - 4));
                writer.Seek(0, SeekOrigin.End);
                break;
            case TileDesc tileDesc:
                WriteSize(9);
                writer.WriteUInt(tileDesc.XSize);
                writer.WriteUInt(tileDesc.YSize);
                writer.WriteByte(tileDesc.Mode);
                break;
            case TimeCode timeCode:
                WriteSize(8);
                writer.WriteUInt(timeCode.TimeAndFlags);
                writer.WriteUInt(timeCode.UserData);
                break;
            case V2i v2i:
                WriteSize(8);
                writer.WriteInt(v2i.V0);
                writer.WriteInt(v2i.V1);
                break;
            case V2f v2f:
                WriteSize(8);
                writer.WriteFloat(v2f.V0);
                writer.WriteFloat(v2f.V1);
                break;
            case V3i v3i:
                WriteSize(12);
                writer.WriteInt(v3i.V0);
                writer.WriteInt(v3i.V1);
                writer.WriteInt(v3i.V2);
                break;
            case V3f v3f:
                WriteSize(12);
                writer.WriteFloat(v3f.V0);
                writer.WriteFloat(v3f.V1);
                writer.WriteFloat(v3f.V2);
                break;
            case ChannelList chlist:
                chlist.WriteTo(writer);
                break;
            case byte[] bytes:
                WriteSize(bytes.Length);
                writer.WriteBytes(bytes);
                break;
            default:
                throw new NotImplementedException($"Writing of attribute {Name} (type: {Type}) not implemented.");
        }
    }
}
