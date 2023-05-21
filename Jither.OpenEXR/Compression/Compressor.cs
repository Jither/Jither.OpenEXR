using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR.Compression;

public abstract class Compressor
{
    public abstract int ScanLinesPerBlock { get; }
    public abstract void Decompress(Stream source, Stream dest);
    public abstract void Compress(Stream source, Stream dest);
}
