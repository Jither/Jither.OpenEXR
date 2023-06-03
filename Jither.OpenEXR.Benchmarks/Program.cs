using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Running;
using Jither.OpenEXR.Benchmarks.Compression;
using Jither.OpenEXR.Compression;

namespace Jither.OpenEXR.Benchmarks;

internal class Program
{
    static void Main()
    {
        BenchmarkRunner.Run<HuffmanBenchmarks>();
    }
}
