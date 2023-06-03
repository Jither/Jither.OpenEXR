using Jither.OpenEXR;
using Jither.OpenEXR.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples;

internal class CreateAndWriteMultiPart : Example
{
    public override string Name => "Create a multi-part file from scratch and write it to disk";
    public override int Order => 3;

    public override void Run()
    {
        const int width = 600;
        const int height = 400;

        // There are some convenience methods for common channel lists, but lists can, of course, also be created from scratch with custom channel names etc.
        var chlist = ChannelList.CreateRGBAFloat(linear: false);

        using (var file = new EXRFile())
        {
            for (int i = 0; i < 3; i++)
            {
                var part = new EXRPart(new Box2i(0, 0, width - 1, height - 1))
                {
                    // Remember, multi-parts must have a name and type for each part.
                    // They can be set through optional parameters on the constructor - or assignment -
                    // as long as they're set before writing.
                    Name = $"Gradient{i}",
                    Type = PartType.ScanLineImage,
                    Compression = EXRCompression.PIZ,
                    Channels = chlist
                };

                // We can add optional attributes:
                part.SetAttribute(AttributeNames.Comments, $"Gradient #{i}");

                // And custom ones:
                part.SetAttribute("application", "Jither.OpenEXR");

                // Add the part information to the file:
                file.AddPart(part);
            }
            // Now we can write the headers:
            file.Write("output-multipart.exr");

            int variation = 0;
            foreach (var part in file.Parts)
            {
                // In order to write the data for our part, we access EXRPart.DataWriter
                Debug.Assert(part.DataWriter != null);

                // OpenEXR stores pixel data with channels separated in each of its chunks. The library provides methods to
                // convert to and from interleaved data.
                byte[] pixelData = CreatePixelData(width, height, variation++);
                part.DataWriter.WriteInterleaved(pixelData, new[] { "R", "G", "B", "A" });
            }
        }
    }

    private static byte[] CreatePixelData(int width, int height, int variation)
    {
        // Creating pixel data for the image. Currently, the library is rather low level for this.
        // Here, we're creating a 256x256 image with RGBA channels, using float (32-bit) data type for each channel.
        float[] image = new float[width * height * 4];

        int index = 0;
        float[] gradient = new float[3];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                gradient[0] = 1f - (float)x / width;
                gradient[1] = (float)y / height;
                gradient[2] = (float)x / width;
                float r = gradient[variation % 3];
                float g = gradient[(variation + 1) % 3];
                float b = gradient[(variation + 2) % 3];
                image[index++] = r;
                image[index++] = g;
                image[index++] = b;
                image[index++] = 1;
            }
        }

        // Again, rather low level - the library only takes byte arrays with the pixel data for now.
        // So, here we convert our data. Rather inefficiently, but good enough for an example.
        // This also assumes little endian architecture.
        byte[] pixelData = new byte[width * height * 4 * sizeof(float)];
        Buffer.BlockCopy(image, 0, pixelData, 0, pixelData.Length);
        return pixelData;
    }
}
