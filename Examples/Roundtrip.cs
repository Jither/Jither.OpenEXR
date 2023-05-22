using Jither.OpenEXR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples;

internal class Roundtrip : Example
{
    public override string Name => "Read a file and write it out with changed settings";
    public override int Order => 2;

    public override void Run()
    {
        using (var file = new EXRFile("images/helmet.exr"))
        {
            var part = file.Parts[0];
            Debug.Assert(part.DataReader != null);

            // We need to read the data into memory and write it back to the new file.
            // Normally, we could just use Read (which reads the raw data) rather than
            // ReadInterleaved. Using ReadInterleaved for testing.
            byte[] pixelData = new byte[part.DataReader.TotalBytes];
            part.DataReader.ReadInterleaved(pixelData, new[] { "R", "G", "B", "A" });

            part.Compression = EXRCompression.RLE;

            file.Write("output-roundtrip.exr");
            
            Debug.Assert(part.DataWriter != null);

            part.DataWriter.WriteInterleaved(pixelData, new[] { "R", "G", "B", "A" });
        }
    }
}