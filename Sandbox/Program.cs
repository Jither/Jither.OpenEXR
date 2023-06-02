using Jither.OpenEXR;
using Jither.OpenEXR.Compression;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox;

internal class Program
{
    static void Main(string[] args)
    {
        using (var file = new EXRFile(@"..\..\..\..\Jither.OpenEXR.Tests\images\openexr-images\Tiles\Ocean.exr"))
        {
            //Console.WriteLine(JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            using (var dest = new EXRFile())
            {
                var partsData = new List<byte[]>();
                foreach (var part in file.Parts)
                {
                    Debug.Assert(part.DataReader != null);

                    var bytes = new byte[part.DataReader.GetTotalByteCount()];
                    part.DataReader.Read(bytes);
                    partsData.Add(bytes);

                    var destPart = new EXRPart(part.DataWindow, name: part.Name, type: PartType.ScanLineImage);
                    destPart.Channels = ChannelList.CreateRGBHalf();
                    dest.AddPart(destPart);
                }
                dest.Write("Ocean-scanline.exr");
                int partIndex = 0;
                foreach (var part in dest.Parts)
                {
                    Debug.Assert(part.DataWriter != null);

                    part.DataWriter.Write(partsData[partIndex++]);
                }
            }
        }
    }
}
