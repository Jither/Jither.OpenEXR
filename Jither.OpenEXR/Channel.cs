namespace Jither.OpenEXR;

public class Channel
{
    /// <summary>
    /// The channel’s name is a text string, for example R, Z or yVelocity. The name tells programs that read the image file how to interpret the data in the channel.
    /// </summary>
    /// <remarks>
    /// For a few channel names, interpretation of the data is predefined:
    /// 
    /// * R = red intensity
    /// * G = green intensity
    /// * B = blue intensity
    /// * A = alpha/opacity: 0.0 means the pixel is transparent; 1.0 means the pixel is opaque.
    /// 
    /// By convention, all color channels are premultiplied by alpha, so that foreground + (1-alpha) xbackground performs a correct “over” operation. 
    /// </remarks>
    public string Name { get; set; }

    /// <summary>
    /// The channel's data type
    /// </summary>
    public EXRDataType Type { get; set; }
    public bool Linear { get; set; }

    /// <summary>
    /// The channel's x sampling rate. Determines for which of the pixels in the image’s data window data are stored in the file. Data for a pixel at pixel space coordinates (x, y)
    /// are only stored if <c>x mod XSampling = 0</c> (and <c>y mod YSampling = 0</c>).
    /// </summary>
    /// <remarks>
    /// For RGBA images, <c>XSampling</c> is 1 for all channels - each channel contains data for every pixel.
    /// </remarks>
    public int XSampling { get; set; }

    /// <summary>
    /// The channel's y sampling rate. Determines for which of the pixels in the image’s data window data are stored in the file. Data for a pixel at pixel space coordinates (x, y)
    /// are only stored if <c>y mod YSampling = 0</c> (and <c>x mod XSampling = 0</c>).
    /// </summary>
    /// <remarks>
    /// For RGBA images, <c>YSampling</c> is 1 for all channels - each channel contains data for every pixel.
    /// </remarks>
    public int YSampling { get; set; }

    public byte Reserved0 { get; set; }
    public byte Reserved1 { get; set; }
    public byte Reserved2 { get; set; }

    public Channel(string name, EXRDataType type, bool linear, int xSampling, int ySampling, byte reserved0 = 0, byte reserved1 = 0, byte reserved2 = 0)
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
