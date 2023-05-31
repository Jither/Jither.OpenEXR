using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Jither.OpenEXR.Compression;

namespace Jither.OpenEXR.Benchmarks.Compression;

[InliningDiagnoser(true, new string[] { "Jither.OpenEXR.Compression" })]
public class HuffmanBenchmarks
{
    private ushort[] noise_16bit(Random rnd, int size)
    {
        var result = new ushort[size];
        for (int i = 0; i < size; i++)
        {
            result[i] = (ushort)(rnd.Next() & 0xffff);
        }
        return result;
    }

    [Benchmark]
    public void HuffmanLongCodes()
    {
        // This exercises the longcode implementation
        var rnd = new Random(1234);

        var uncompressed = noise_16bit(rnd, 17000);
        var compressed = HuffmanCoding.Compress(uncompressed);
        var decompressed = new ushort[uncompressed.Length];
        HuffmanCoding.Decompress(compressed, decompressed, compressed.Length);
    }
}
