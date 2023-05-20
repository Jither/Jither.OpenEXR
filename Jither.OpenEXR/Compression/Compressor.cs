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

public class NullCompressor : Compressor
{
    public override int ScanLinesPerBlock => 1;

    public override void Compress(Stream source, Stream dest)
    {
        source.CopyTo(dest);
    }

    public override void Decompress(Stream source, Stream dest)
    {
        source.CopyTo(dest);
    }
}

public class RLECompressor : Compressor
{
    public override int ScanLinesPerBlock => 1;

    public override void Compress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }

    public override void Decompress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }
}

public class ZipCompressor : Compressor
{
    public override int ScanLinesPerBlock => 16;

    public override void Compress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }

    public override void Decompress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }
}

public class ZipSCompressor : Compressor
{
    public override int ScanLinesPerBlock => 1;

    public override void Compress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }

    public override void Decompress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }
}

public class PizCompressor : Compressor
{
    public override int ScanLinesPerBlock => 32;

    public override void Compress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }

    public override void Decompress(Stream source, Stream dest)
    {
        throw new NotImplementedException();
    }
}
