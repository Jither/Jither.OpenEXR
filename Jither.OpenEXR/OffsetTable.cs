using System.Collections;

namespace Jither.OpenEXR;

public class OffsetTable : List<ulong>
{
    public OffsetTable()
    {
    }

    public OffsetTable(int capacity) : base(capacity)
    {
    }

    public static OffsetTable ReadFrom(EXRReader reader, int count)
    {
        var result = new OffsetTable(count);
        for (int i = 0; i < count; i++)
        {
            result.Add(reader.ReadULong());
        }
        return result;
    }
}