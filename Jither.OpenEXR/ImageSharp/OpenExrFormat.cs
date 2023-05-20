using SixLabors.ImageSharp.Formats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR.ImageSharp;

public sealed class OpenExrFormat : IImageFormat<OpenExrMetadata>
{
    private OpenExrFormat()
    {

    }

    public static OpenExrFormat Instance { get; } = new OpenExrFormat();

    public string Name => "OpenEXR";

    public string DefaultMimeType => "image/x-exr";

    public IEnumerable<string> MimeTypes => OpenExrConstants.MimeTypes;

    public IEnumerable<string> FileExtensions => OpenExrConstants.FileExtensions;

    public OpenExrMetadata CreateDefaultFormatMetadata() => new();
}
