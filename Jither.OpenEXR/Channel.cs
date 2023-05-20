namespace Jither.OpenEXR;

public class Channel
{
    public string Name { get; set; }
    public PixelType Type { get; set; }
    public bool Linear { get; set; }
    public int XSampling { get; set; }
    public int YSampling { get; set; }

    public byte Reserved0 { get; set; }
    public byte Reserved1 { get; set; }
    public byte Reserved2 { get; set; }

    public Channel(string name, PixelType type, bool linear, int xSampling, int ySampling, byte reserved0 = 0, byte reserved1 = 0, byte reserved2 = 0)
    {
        Name = name;
        Type = type;
        Linear = linear;
        XSampling = xSampling;
        YSampling = ySampling;
        Reserved0 = reserved0;
        Reserved1 = reserved1;
        Reserved2 = reserved2;
    }
}
