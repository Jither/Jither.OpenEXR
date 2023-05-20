using Jither.OpenEXR;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox;

internal class Program
{
    static void Main(string[] args)
    {
        using (var file = EXRFile.FromFile(args[0]))
        {
            Console.WriteLine(JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            using (var output = new FileStream(@"D:\test.raw", FileMode.Create, FileAccess.Write))
            {
                file.SaveRaw(output, new[] { "R", "G", "B", "A" });
            }
            file.SaveAs(@"D:\test.exr");
        }
    }
}
