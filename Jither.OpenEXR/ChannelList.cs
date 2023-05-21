using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR;

public class ChannelList : IReadOnlyList<Channel>
{
    private readonly List<Channel> channels = new();

    public Channel this[int index] => channels[index];

    public Channel? this[string name] => channels.SingleOrDefault(c => c.Name == name);

    public int Count => channels.Count;

    public int IndexOf(string name)
    {
        return channels.FindIndex(c => c.Name == name);
    }

    public static ChannelList ReadFrom(EXRReader reader, int size)
    {
        long totalSize = 0;

        void CheckSize()
        {
            if (totalSize > size)
            {
                throw new EXRFormatException($"Read {totalSize} in channel list, but declared size was {size}");
            }
        }

        var result = new ChannelList();
        long bytesRead;
        while (ReadChannel(reader, out var channel, out bytesRead))
        {
            Debug.Assert(channel != null, "ReadChannel returned true, although channel is null!");
            result.channels.Add(channel);
            totalSize += bytesRead;

            CheckSize();
        }
        totalSize += bytesRead;
        CheckSize();

        return result;
    }

    public void WriteTo(EXRWriter writer)
    {
        writer.WriteInt(
            channels.Count * 16 + // Numeric values
            channels.Sum(c => c.Name.Length + 1) // Null-terminated names
            + 1 // Channel null terminator
            );

        foreach (var channel in channels)
        {
            writer.WriteStringZ(channel.Name);
            writer.WriteInt((int)channel.Type);
            writer.WriteByte(channel.Linear ? (byte)1 : (byte)0);
            writer.WriteInt(channel.XSampling);
            writer.WriteInt(channel.YSampling);
            writer.WriteByte(0);
            writer.WriteByte(0);
            writer.WriteByte(0);
        }
        writer.WriteByte(0);
    }

    private static bool ReadChannel(EXRReader reader, out Channel? channel, out long bytesRead)
    {
        var start = reader.Position;
        var name = reader.ReadStringZ(255);
        if (name == "")
        {
            channel = null;
            bytesRead = reader.Position - start;
            return false;
        }

        channel = new Channel(
            name, 
            (EXRDataType)reader.ReadInt(),
            linear: reader.ReadByte() != 0,
            xSampling: reader.ReadInt(),
            ySampling: reader.ReadInt(),
            reserved0: reader.ReadByte(),
            reserved1: reader.ReadByte(),
            reserved2: reader.ReadByte()
        );

        bytesRead = reader.Position - start;
        return true;
    }

    public IEnumerator<Channel> GetEnumerator()
    {
        return channels.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
