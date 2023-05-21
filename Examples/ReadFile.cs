using Jither.OpenEXR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples;

internal class ReadFile : Example
{
    public override string Name => "Read a file and retrieve header information";
    public override int Order => 1;

    public override void Run()
    {
        using (var file = new EXRFile("images/helmet.exr"))
        {
            Debug.Assert(file.OriginalVersion != null);

            Output("File version", file.OriginalVersion.Number);
            Output("Is multi-part", file.OriginalVersion.IsMultiPart);
            Output("Is tiled single-part", file.OriginalVersion.IsSinglePartTiled);
            Output("Has deep data", file.OriginalVersion.HasNonImageParts);
            Output("Has long names", file.OriginalVersion.HasLongNames);
            Output("Flags", file.OriginalVersion.Flags);
            Output();

            Output("Parts:");
            foreach (var part in file.Parts)
            {
                Output($"  {part.Name ?? "[unnamed]"}");

                foreach (var attr in part.Attributes)
                {
                    Output($"    {attr.Name}", attr.UntypedValue ?? "[null]");
                }
            }
        }
    }
}
