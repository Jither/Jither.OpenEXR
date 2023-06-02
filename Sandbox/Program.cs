using Jither.OpenEXR;
using Jither.OpenEXR.Compression;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox;

internal class Program
{
    static void Main()
    {
        using (var file = new EXRFile(@"../../../../Jither.OpenEXR.Tests/images/openexr-images/MultiResolution/Bonita.exr"))
        {
            Console.WriteLine(JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            using (var dest = new EXRFile())
            {
                var partsData = new List<byte[]>();
                foreach (var part in file.Parts)
                {
                    Debug.Assert(part.DataReader != null);

                    Debug.Assert(part.IsTiled);
                    var level = part.TilingInformation.GetLevel(1, 1);
                    var bytes = new byte[level.TotalByteCount];
                    part.DataReader.Read(bytes, level.LevelX, level.LevelY);
                    partsData.Add(bytes);

                    var destPart = new EXRPart(level.DataWindow, name: part.Name, type: PartType.ScanLineImage);
                    destPart.Channels = ChannelList.CreateRGBHalf();
                    dest.AddPart(destPart);
                }
                dest.Write("Bonita-scanline.exr");
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
