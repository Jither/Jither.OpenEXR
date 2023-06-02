using Jither.OpenEXR.Attributes;
using Jither.OpenEXR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples;

internal class ConvertMultiResolutionTiledToMultiPartScanline : Example
{
    public override string Name => "Convert a multi-resolution tiled file to a multi-part scanline file";
    public override int Order => 4;

    // IMPORTANT: djv doesn't support multipart files, and mrViewer won't actually display (what it calls) layers with different sizes.
    // To view the output of this example, use e.g. the EXR-IO plugin for Photoshop. This example is mainly a test of Jither.OpenEXR reading
    // all resolutions of a multi-resolution file properly.
    public override void Run()
    {
        using (var inputFile = new EXRFile("../../../../Jither.OpenEXR.Tests/images/openexr-images/MultiResolution/Bonita.exr"))
        {
            var levelsData = new List<byte[]>();

            // The input image only has one part - no need to iterate.
            var inputPart = inputFile.Parts[0];
            
            Debug.Assert(inputPart.IsTiled);
            
            using (var outputFile = new EXRFile())
            {
                int levelIndex = 0;
                foreach (var level in inputPart.TilingInformation.Levels)
                {
                    var levelByteSize = level.TotalByteCount;
                    byte[] levelData = new byte[levelByteSize];
                    inputPart.DataReader.Read(levelData, level);
                    levelsData.Add(levelData);

                    // At some point, there might be a convenience method to copy attributes etc.
                    // Multi part files need a unique name for each part.
                    // DisplayWindow must be the same for all parts in OpenEXR (it's a "shared attribute").
                    // Write() (called further down) will validate this, and throw if shared attributes differ between parts.
                    // We'll just reuse the input part's display window.
                    var outputPart = new EXRPart(new Box2i(level.DataWindow), inputPart.DisplayWindow, name: $"Resolution {levelIndex}", type: PartType.ScanLineImage);
                    outputPart.Channels = inputPart.Channels;
                    outputPart.Compression = EXRCompression.ZIP;

                    outputFile.AddPart(outputPart);

                    levelIndex++;
                }

                outputFile.Write("output-converted.exr");

                int dataIndex = 0;
                foreach (var outputPart in outputFile.Parts)
                {
                    outputPart.DataWriter.Write(levelsData[dataIndex++]);
                }
            }
        }
    }
}