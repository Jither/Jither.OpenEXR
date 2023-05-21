using Jither.OpenEXR;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox;

internal class Program
{
    static void Main(string[] args)
    {
        using (var file = new EXRFile(args[0]))
        {
            Console.WriteLine(JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            var partsData = new List<byte[]>();
            foreach (var part in file.DataReaders)
            {
                var bytes = new byte[part.TotalBytes];
                part.Read(bytes);
                partsData.Add(bytes);
            }
            
            file.ForceVersion2 = true;
            file.Parts[0].Compression = EXRCompression.RLE;
            
            file.Write(@"D:\test-rle.exr");
            int partIndex = 0;
            foreach (var part in file.DataWriters)
            {
                part.Write(partsData[partIndex++]);
            }
        }
    }
}
