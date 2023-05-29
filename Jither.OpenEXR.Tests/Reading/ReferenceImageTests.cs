using Jither.OpenEXR.Attributes;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR.Reading;

public class ReferenceImageTests
{
    private const string BasePath = "../../../images/openexr-images/";

    private void TestReadParts(IEnumerable<EXRPart> parts)
    {
        foreach (var part in parts)
        {
            var pixelData = ArrayPool<byte>.Shared.Rent(part.DataReader.GetTotalByteCount());
            try
            {
                // TODO: Check that the data we read is actually valid. For now, we just check that it's read succesfully.
                part.DataReader.Read(pixelData);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pixelData);
            }
        }
    }

    [Theory]
    [InlineData("Beachball/multipart.0001.exr")]
    [InlineData("Beachball/multipart.0002.exr")]
    [InlineData("Beachball/multipart.0003.exr")]
    [InlineData("Beachball/multipart.0004.exr")]
    [InlineData("Beachball/multipart.0005.exr")]
    [InlineData("Beachball/multipart.0006.exr")]
    [InlineData("Beachball/multipart.0007.exr")]
    [InlineData("Beachball/multipart.0008.exr")]
    public void Reads_reference_image_Beachball_multipart(string imagePath)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(2, file.OriginalVersion.Number);
            Assert.True(file.OriginalVersion.IsMultiPart);
            Assert.False(file.OriginalVersion.IsSinglePartTiled);
            Assert.False(file.OriginalVersion.HasNonImageParts);
            Assert.False(file.OriginalVersion.HasLongNames);

            Assert.Equal(10, file.Parts.Count);
            Assert.Equal(new[]
            {
                "rgba_right",
                "depth_left",
                "forward_left",
                "whitebarmask_left",
                "rgba_left",
                "depth_right",
                "forward_right",
                "disparityL",
                "disparityR",
                "whitebarmask_right"
            }, file.PartNames);

            Assert.All(file.Parts, (p, index) =>
            {
                Assert.Equal(EXRCompression.ZIPS, p.Compression);
                Assert.Equal(LineOrder.IncreasingY, p.LineOrder);
                Assert.Equal(PartType.ScanLineImage, p.Type);
                Assert.Equal(new Box2i(0, 0, 2047, 1555), p.DisplayWindow);
                // DataWindow differs between these files (that's much of their point)

                Assert.Equal(1, p.PixelAspectRatio);
                Assert.False(p.IsTiled);
            });

            Assert.True(file.Parts[0].IsRGB);
            Assert.True(file.Parts[0].HasAlpha);
            Assert.Equal(file.Parts[0], file.PartsByName["rgba_right"]);
            Assert.Equal(file.Parts[4], file.PartsByName["rgba_left"]);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("Beachball/singlepart.0001.exr")]
    [InlineData("Beachball/singlepart.0002.exr")]
    [InlineData("Beachball/singlepart.0003.exr")]
    [InlineData("Beachball/singlepart.0004.exr")]
    [InlineData("Beachball/singlepart.0005.exr")]
    [InlineData("Beachball/singlepart.0006.exr")]
    [InlineData("Beachball/singlepart.0007.exr")]
    [InlineData("Beachball/singlepart.0008.exr")]
    public void Reads_reference_image_Beachball_singlepart(string imagePath)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(2, file.OriginalVersion.Number);
            Assert.False(file.OriginalVersion.IsMultiPart);
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];
            // Instead of multiple parts, these images hold the data in additional channels
            Assert.Equal(20, part.Channels.Count);
            Assert.Equal(new[]
            {
                "A",
                "B",
                "G",
                "R",
                "Z",
                "disparityL.x",
                "disparityL.y",
                "disparityR.x",
                "disparityR.y",
                "forward.left.u",
                "forward.left.v",
                "forward.right.u",
                "forward.right.v",
                "left.A",
                "left.B",
                "left.G",
                "left.R",
                "left.Z",
                "whitebarmask.left.mask",
                "whitebarmask.right.mask"
            }, part.Channels.Names);
            Assert.Equal(EXRCompression.ZIPS, part.Compression);
            Assert.Equal(LineOrder.IncreasingY, part.LineOrder);
            Assert.Equal(PartType.ScanLineImage, part.Type);
            Assert.Equal(new Box2i(0, 0, 2047, 1555), part.DisplayWindow);
            // DataWindow differs between these files (that's much of their point)
            Assert.Equal(1, part.PixelAspectRatio);
            Assert.False(part.IsTiled);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("Scanlines/CandleGlass.exr", 999, 809)]
    [InlineData("Scanlines/Desk.exr", 643, 873)]
    [InlineData("Scanlines/MtTamWest.exr", 1213, 731)]
    [InlineData("Scanlines/PrismsLenses.exr", 1199, 864)]
    [InlineData("Scanlines/StillLife.exr", 1239, 845)]
    [InlineData("Scanlines/Tree.exr", 927, 905)]
    public void Reads_reference_image_Scanlines(string imagePath, int expectedXMax, int expectedYMax)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(2, file.OriginalVersion.Number);
            Assert.False(file.OriginalVersion.IsMultiPart);
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];
            // Instead of multiple parts, these images hold the data in additional channels
            Assert.Equal(4, part.Channels.Count);

            Assert.Equal(new[] { "A", "B", "G", "R" }, part.Channels.Names);

            Assert.Equal(EXRCompression.PIZ, part.Compression);
            Assert.Equal(LineOrder.IncreasingY, part.LineOrder);
            // These images don't specify their part type - since they're not SinglePartTiled, they're Scanline
            Assert.Equal(PartType.Unknown, part.Type);
            Assert.False(part.IsTiled);

            Assert.Equal(new Box2i(0, 0, expectedXMax, expectedYMax), part.DisplayWindow);
            // DataWindow differs between these files (that's much of their point)
            Assert.Equal(1, part.PixelAspectRatio);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("Scanlines/Cannon.exr")]
    public void Fails_attempting_to_read_unsupported_compression(string imagePath)
    {
        var path = Path.Combine(BasePath, imagePath);
        // TODO: Should actually allow reading headers.
        Assert.Throws<NotSupportedException>(() => new EXRFile(path));
    }

    [Theory]
    [InlineData("Scanlines/Blobbies.exr")]
    public void Reads_reference_image_Scanlines_Blobbies(string imagePath)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(2, file.OriginalVersion.Number);
            Assert.False(file.OriginalVersion.IsMultiPart);
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];
            // Instead of multiple parts, these images hold the data in additional channels
            Assert.Equal(5, part.Channels.Count);

            Assert.Equal(new[] { "A", "B", "G", "R", "Z" }, part.Channels.Names);

            Assert.Equal(EXRCompression.ZIP, part.Compression);
            Assert.Equal(LineOrder.DecreasingY, part.LineOrder);
            // These images don't specify their part type - since they're not SinglePartTiled, they're Scanline
            Assert.Equal(PartType.Unknown, part.Type);
            Assert.False(part.IsTiled);

            Assert.Equal(new Box2i(0, 0, 999, 999), part.DisplayWindow);
            // DataWindow differs between these files (that's much of their point)
            Assert.Equal(1, part.PixelAspectRatio);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("TestImages/BrightRingsNanInf.exr")]
    public void Reads_image_with_non_finite_pixel_values(string imagePath)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(2, file.OriginalVersion.Number);
            Assert.False(file.OriginalVersion.IsMultiPart);
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];
            // Instead of multiple parts, these images hold the data in additional channels
            Assert.Equal(3, part.Channels.Count);

            Assert.Equal(new[] { "B", "G", "R" }, part.Channels.Names);

            Assert.Equal(EXRCompression.ZIP, part.Compression);
            Assert.Equal(LineOrder.IncreasingY, part.LineOrder);
            Assert.Equal(PartType.Unknown, part.Type);
            Assert.False(part.IsTiled);

            Assert.Equal(new Box2i(0, 0, 799, 799), part.DisplayWindow);
            // DataWindow differs between these files (that's much of their point)
            Assert.Equal(1, part.PixelAspectRatio);

            TestReadParts(file.Parts);
        }
    }
}
