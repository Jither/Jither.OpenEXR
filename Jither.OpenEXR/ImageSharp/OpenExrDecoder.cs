using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR.ImageSharp;

/*
public class OpenExrDecoder : SpecializedImageDecoder<OpenExrDecoderOptions>
{
    protected override OpenExrDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options) => new OpenExrDecoderOptions { GeneralOptions = options };

    protected override Image<TPixel> Decode<TPixel>(OpenExrDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using (var file = EXRFile.FromStream(stream))
        {
            var part = file.Parts[0];
            var image = new Image<TPixel>(part.DataWindow.Width, part.DataWindow.Height);

            Buffer2D<TPixel> pixels = image.Frames.RootFrame.PixelBuffer;

            part.Decode()
        }
    }

    protected override Image Decode(OpenExrDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        using (var file = EXRFile.FromStream(stream))
        {
            var part = file.Parts[0];
            return new ImageInfo(
                new PixelTypeInfo(part.BitsPerPixel, part.HasAlpha ? PixelAlphaRepresentation.None : PixelAlphaRepresentation.Associated),
                new Size(part.DataWindow.Width, part.DataWindow.Height),
                new ImageMetadata
                {
                    ResolutionUnits = PixelResolutionUnit.AspectRatio,
                    HorizontalResolution = part.DataWindow.Width * part.PixelAspectRatio,
                    VerticalResolution = part.DataWindow.Height
                });
        }
    }
}

public class OpenExrDecoderOptions : ISpecializedDecoderOptions
{
    public DecoderOptions GeneralOptions { get; init; } = new();
}
*/