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
        using (var file = new EXRFile(@"D:\Downloads\zips.exr"))
        {
            Console.WriteLine(JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            var partsData = new List<byte[]>();
            foreach (var part in file.Parts)
            {
                Debug.Assert(part.DataReader != null);

                var bytes = new byte[part.DataReader.GetTotalByteCount()];
                part.DataReader.Read(bytes);
                partsData.Add(bytes);
            }

            file.ForceVersion2 = true;
            file.Parts[0].Compression = EXRCompression.ZIPS;

            file.Write(@"D:\test-zips.exr");
            int partIndex = 0;
            foreach (var part in file.Parts)
            {
                Debug.Assert(part.DataWriter != null);

                part.DataWriter.Write(partsData[partIndex++]);
            }
        }
    }
}
