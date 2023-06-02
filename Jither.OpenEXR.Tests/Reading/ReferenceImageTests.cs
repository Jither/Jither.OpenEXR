using Jither.OpenEXR.Attributes;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR.Reading;

public class ReferenceImageTests
{
    private const string BasePath = "../../../images/openexr-images/";

    private static void TestReadParts(IEnumerable<EXRPart> parts)
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
    [InlineData("Chromaticities/Rec709.exr")]
    [InlineData("Chromaticities/Rec709_YC.exr", true)]
    [InlineData("Chromaticities/XYZ.exr")]
    [InlineData("Chromaticities/XYZ_YC.exr", true)]
    public void Reads_reference_image_Chromaticities(string imagePath, bool isYC = false)
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

            Assert.Equal(isYC ? new[] { "BY", "RY", "Y" } : new[] { "B", "G", "R" }, part.Channels.Names);

            Assert.Equal(EXRCompression.PIZ, part.Compression);
            Assert.Equal(LineOrder.IncreasingY, part.LineOrder);
            // These images don't specify their part type - since they're not SinglePartTiled, they're Scanline
            Assert.Equal(PartType.Unknown, part.Type);
            Assert.False(part.IsTiled);

            Assert.Equal(new Box2i(0, 0, 609, 405), part.DisplayWindow);
            Assert.Equal(1, part.PixelAspectRatio);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("LuminanceChroma/Garden.exr", 873, 492, true, true)]
    [InlineData("LuminanceChroma/MtTamNorth.exr", 1197, 795)]
    [InlineData("LuminanceChroma/StarField.exr", 999, 999)]
    public void Reads_reference_image_LuminanceChroma(string imagePath, int expectedXMax, int expectedYMax, bool luminanceOnly = false, bool isTiled = false)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(2, file.OriginalVersion.Number);
            Assert.False(file.OriginalVersion.IsMultiPart);
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];
            // Instead of multiple parts, these images hold the data in additional channels
            Assert.Equal(luminanceOnly ? 1 : 3, part.Channels.Count);

            Assert.Equal(luminanceOnly ? new[] { "Y" } : new[] { "BY", "RY", "Y" }, part.Channels.Names);

            Assert.Equal(EXRCompression.PIZ, part.Compression);
            Assert.Equal(LineOrder.IncreasingY, part.LineOrder);
            // These images don't specify their part type - since they're not SinglePartTiled, they're Scanline
            Assert.Equal(PartType.Unknown, part.Type);

            // "Garden.exr" is tiled but not multipart - hence it's indicated through the version bits.
            Assert.Equal(isTiled, file.OriginalVersion.IsSinglePartTiled);
            Assert.Equal(isTiled, part.IsTiled);

            Assert.Equal(new Box2i(0, 0, expectedXMax, expectedYMax), part.DisplayWindow);
            Assert.Equal(1, part.PixelAspectRatio);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("DisplayWindow/t01.exr", 0, 0, 399, 299)]
    [InlineData("DisplayWindow/t02.exr", 1, 1, 400, 300)]
    [InlineData("DisplayWindow/t03.exr", 30, 20, 399, 299)]
    [InlineData("DisplayWindow/t04.exr", 0, 0, 369, 279)]
    [InlineData("DisplayWindow/t05.exr", 30, 20, 369, 279)]
    [InlineData("DisplayWindow/t06.exr", -1, -1, 400, 300)]
    [InlineData("DisplayWindow/t07.exr", -40, -40, 440, 330)]
    [InlineData("DisplayWindow/t08.exr", 0, 0, 500, 400, 30, 40, 429, 339)]
    [InlineData("DisplayWindow/t09.exr", 400, 0, 599, 299)]
    [InlineData("DisplayWindow/t10.exr", -100, 0, -1, 299)]
    [InlineData("DisplayWindow/t11.exr", 0, 300, 399, 499)]
    [InlineData("DisplayWindow/t12.exr", 0, -100, 399, -1)]
    [InlineData("DisplayWindow/t13.exr", 399, 299, 499, 399)]
    [InlineData("DisplayWindow/t14.exr", -100, -100, 0, 0)]
    [InlineData("DisplayWindow/t15.exr", -40, -40, 440, 330)]
    [InlineData("DisplayWindow/t16.exr", -40, -40, 440, 330)]
    public void Reads_reference_image_DisplayWindow(string imagePath, int expectedXMin, int expectedYMin, int expectedXMax, int expectedYMax, int expectedDataXMin = 0, int expectedDataYMin = 0, int expectedDataXMax = 399, int expectedDataYMax = 299)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];

            Assert.Equal(new Box2i(expectedXMin, expectedYMin, expectedXMax, expectedYMax), part.DisplayWindow);
            Assert.Equal(new Box2i(expectedDataXMin, expectedDataYMin, expectedDataXMax, expectedDataYMax), part.DataWindow);

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
            Assert.Equal(1, part.PixelAspectRatio);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("Tiles/GoldenGate.exr", new[] { "B", "G", "R" }, EXRCompression.PIZ)]
    [InlineData("Tiles/Ocean.exr", new[] { "B", "G", "R" }, EXRCompression.ZIP)]
    [InlineData("Tiles/Spirals.exr", new[] { "A", "B", "G", "R", "Z" }, EXRCompression.PXR24)]
    public void Reads_reference_image_Tiles(string imagePath, string[] channels, EXRCompression compression)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(2, file.OriginalVersion.Number);
            Assert.False(file.OriginalVersion.IsMultiPart);
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];
            // Instead of multiple parts, these images hold the data in additional channels
            Assert.Equal(channels.Length, part.Channels.Count);

            Assert.Equal(channels, part.Channels.Names);

            Assert.Equal(compression, part.Compression);
            Assert.Equal(LineOrder.IncreasingY, part.LineOrder);
            // These images don't specify their part type - but mark SinglePartTiled
            Assert.Equal(PartType.Unknown, part.Type);
            Assert.True(part.IsTiled);

            Assert.Equal(1, part.PixelAspectRatio);

            if (compression != EXRCompression.PXR24)
            {
                TestReadParts(file.Parts);
            }
        }
    }

    [Theory]
    [InlineData("Scanlines/Cannon.exr")]
    [InlineData("LuminanceChroma/CrissyField.exr")]
    [InlineData("LuminanceChroma/Flowers.exr")]
    public void Allows_reading_headers_for_unsupported_compression(string imagePath)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Equal(1, file.Parts.Count);
            var part = file.Parts[0];
            Assert.Equal(EXRCompression.B44, part.Compression);
        }
    }

    [Theory]
    [InlineData("Scanlines/Cannon.exr")]
    [InlineData("LuminanceChroma/CrissyField.exr")]
    [InlineData("LuminanceChroma/Flowers.exr")]
    public void Fails_attempting_to_read_data_for_unsupported_compression(string imagePath)
    {
        var path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Throws<NotSupportedException>(() => TestReadParts(file.Parts));
        }
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
            Assert.Equal(1, part.PixelAspectRatio);

            TestReadParts(file.Parts);
        }
    }

    [Theory]
    [InlineData("Damaged/asan_heap-oob_4cb169_255_cc7ac9cde4b8634b31cb41c8fe89b92d_exr")]
    [InlineData("Damaged/asan_heap-oob_4cb169_380_4572f174dd4e48b879ca6d516486f30e_exr")]
    [InlineData("Damaged/asan_heap-oob_504235_131_78b0e15388401193e1ee2ce20fb7f3b0_exr")]
    [InlineData("Damaged/asan_heap-oob_7efd58fcbf3d_356_668b128c27c62e0d8314c503831fde88_exr")]
    [InlineData("Damaged/asan_heap-oob_7f09128c0ec1_358_ecaca7fb3f230d9d842074e1dd88f29b_exr")]
    [InlineData("Damaged/asan_heap-oob_7f11ece681f1_785_a570a0a25ada4752dabb554ad6b1eb6b_exr")]
    [InlineData("Damaged/asan_heap-oob_7f178ac80539_109_519f88e9ededfff61f535d6c9eb25a85_exr")]
    [InlineData("Damaged/asan_heap-oob_7f35311a1426_780_4871d40882e0fe7fae1427a82319e144_exr")]
    [InlineData("Damaged/asan_heap-oob_7f4f5558d00e_414_ec6445a8638a21c93ce8feb5a2e71981_exr")]
    [InlineData("Damaged/asan_heap-oob_7f54ed80df53_321_19490ab1841d3854eec557f3c23d0db0_exr")]
    [InlineData("Damaged/asan_heap-oob_7f5860f8d1f4_188_2e62008f8ecb3bb2ed67c09fa0939da7_exr")]
    [InlineData("Damaged/asan_heap-oob_7f58e8d75e8b_186_4bb7b1de93e9e44ea921c301b29a8026_exr")]
    [InlineData("Damaged/asan_heap-oob_7f5c18182f27_369_1fb4dae7654c77cd79646d3aa049d5dd_exr")]
    [InlineData("Damaged/asan_heap-oob_7f5cdab9a3a7_415_c33b838f08aafc976d3c24139936e122_exr")]
    [InlineData("Damaged/asan_heap-oob_7f6a78657cbf_916_10cc35387b54fdc7744f91b5bb424009_exr")]
    [InlineData("Damaged/asan_heap-oob_7f6e5e983398_203_ff7c0c73c79483db1fee001d77463c37_exr")]
    [InlineData("Damaged/asan_heap-oob_7f730474b07c_543_fb506af38c88894d92ba0d433cf41abc_exr")]
    [InlineData("Damaged/asan_heap-oob_7f76b2c2cefb_196_ea4f8db8b4f2c11e02428c08e9bbbbb8_exr")]
    [InlineData("Damaged/asan_heap-oob_7f7a75f9abf5_577_a2b8668cc6069643543cb80fedca3ee4_exr")]
    [InlineData("Damaged/asan_heap-oob_7f8170c1abfa_115_8c6e33969541bf432ef7e68cc369728c_exr")]
    [InlineData("Damaged/asan_heap-oob_7f8dd48421cd_321_7bae35650e908b12dbee1cf01e3d357f_exr")]
    [InlineData("Damaged/asan_heap-oob_7f8ed39ceed3_955_c6bb655a1bbfab9c5b511bd2b767e023_exr")]
    [InlineData("Damaged/asan_heap-oob_7f9acb068ee5_177_ec645ad270202d39ba5e80c826bbf13d_exr")]
    [InlineData("Damaged/asan_heap-oob_7f9cc08a96a5_942_e708072e479264a7808c055094a0fed9_exr")]
    [InlineData("Damaged/asan_heap-oob_7fa0e1f48cbf_760_be9901248390240a24449d4e8a97f6f2_exr")]
    [InlineData("Damaged/asan_heap-oob_7fb97d097381_293_78e73b6494a955e87faabcb16b35faa0_exr")]
    [InlineData("Damaged/asan_heap-oob_7fc6b05f1eaf_255_e967badfa747d1bb0eda71a1883b419e_exr")]
    [InlineData("Damaged/asan_heap-oob_7fca10855564_529_6d418eae3e33a819185a8b09c40fd123_exr")]
    [InlineData("Damaged/asan_heap-oob_7fcdcb0a9f65_100_70d0d5b98567a5174d512dba7a603377_exr")]
    [InlineData("Damaged/asan_heap-oob_7fce901e7498_737_927b67c9a1ecd5f997d3a2620fdbf639_exr")]
    [InlineData("Damaged/asan_heap-oob_7fd328d0d206_535_9cc3d65c368fb138cb6a4bdd4da8070f_exr")]
    [InlineData("Damaged/asan_heap-oob_7fd921b41f11_369_da245dc772c0a5a60ce7b759b2132c51_exr")]
    [InlineData("Damaged/asan_heap-oob_7fe4c4caa975_277_153f78ec07237d01e8902143da28c7ec_exr")]
    [InlineData("Damaged/asan_heap-oob_7fe814d1ce9d_617_e00af1c4c76b988122b68d43b073dd47_exr")]
    [InlineData("Damaged/asan_heap-oob_7ff4b88b21df_560_e119e0ba5bf9345e7daa85cc2fff6684_exr")]
    [InlineData("Damaged/asan_stack-oob_433d4f_157_4c56ecca982bc10e976527cd314bbcfa_exr")]
    [InlineData("Damaged/asan_stack-oob_433d4f_510_f8d3ef49bd6e5f7ca4fb7e0f9c0af139_exr")]
    [InlineData("Damaged/autofuzz_141647077")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deepscanlines_fuzzer-5642309123964928")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deepscanlines_fuzzer-5706205037854720")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deepscanlines_fuzzer-5742768392241152")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deepscanlines_fuzzer-5762410275930112")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deepscanlines_fuzzer-6200187410972672")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deepscanlines_fuzzer-6279893151907840")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deeptiles_fuzzer-4690719340756992")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deeptiles_fuzzer-4850238502993920")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deeptiles_fuzzer-5078480846585856")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deeptiles_fuzzer-5663281278353408")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_deeptiles_fuzzer-6540114071912448")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-4598960264183808")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-4675763792117760")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-4755804284649472")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-4807746560065536")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-4817036465274880")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-4817797211357184")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-4868768916439040")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5067980763430912")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5109751098769408")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5232906122428416")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5367816090943488")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5406325370650624")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5446594692513792")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5539187979845632")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5546362875412480")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5563041179631616")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5594942305075200")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5656865489944576")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5665195584258048")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5674515140706304")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5692665448497152")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5714952583249920")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5732453365972992")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5799915378835456.fuzz")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5810996882702336.fuzz")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5870109562372096")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5968486906068992")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-5980233544105984")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-6276025044566016")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-6305658012041216")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-6309948248162304")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-6318802980700160")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrcheck_fuzzer-6439257692569600")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-4924251398995968")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5134597135007744")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5155928358518784")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5157125555486720")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5175124111917056")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5394528239091712.fuzz")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5661107218546688")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5672621044400128")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5682822034227200")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5710558750572544")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5733310685511680")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5762859548803072")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5896229264031744")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-5928077310558208")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-6028795828764672")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-6210287474311168")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-6218676477624320")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-6265093673975808")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-6268549191172096")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrenvmap_fuzzer-6304609308770304")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrheader_fuzzer-5148685380616192")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrheader_fuzzer-5714896743038976")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_exrheader_fuzzer-6329404817670144")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5087823085174784")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5099738956038144")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5117127072415744")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5134807410147328")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5143852667895808")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5153119208734720")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5647879652507648.fuzz")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5663001244598272")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5714808157241344")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5715033768853504")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5764286049419264")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-5980953682640896")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-6196957322936320")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-6207325210411008")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-6384011338055680")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-6432350251253760")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-6481309109846016")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_scanlines_fuzzer-6505325849739264")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_tiles_fuzzer-5131789849591808")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_tiles_fuzzer-5139170063024128")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_tiles_fuzzer-5434189368000512.fuzz")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_tiles_fuzzer-5767570687524864")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_tiles_fuzzer-5895515179581440")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_tiles_fuzzer-5936913343840256")]
    [InlineData("Damaged/clusterfuzz-testcase-minimized-openexr_tiles_fuzzer-6295699059376128")]
    [InlineData("Damaged/clusterfuzz-testcase-openexr_deepscanlines_fuzzer-5742768392241152")]
    [InlineData("Damaged/clusterfuzz-testcase-openexr_exrcheck_fuzzer-4598960264183808")]
    [InlineData("Damaged/heap_overflow_exr")]
    [InlineData("Damaged/NULL_pointer")]
    [InlineData("Damaged/oom_exr")]
    [InlineData("Damaged/openexr_2.2.0_memory_allocation_error_1_exr")]
    [InlineData("Damaged/openexr_2.5.1_null_deref_error")]
    [InlineData("Damaged/poc-099125d15685ef30feae813ddae15406b3f539d7cc4aa399176a50dcfe9be95c")]
    [InlineData("Damaged/poc-2b68475a090117f033df54c2de31546f7a1927ecadd9d0aa9b6bb8daad8ea971_min")]
    [InlineData("Damaged/poc-3e54bd90fc0e2a0b348ecd80d96738ed8324de388b3e88fd6c5fc666c2f01d83_min")]
    [InlineData("Damaged/poc-89f7735a7cc9dcee59bfce4da70ad12e35a8809945b01b975d1a52ec15dbeccc")]
    [InlineData("Damaged/poc-9651abd6ee953b9f669db5927f8832f1b1eab028fa6ae7b4176a701aeea0ec90")]
    [InlineData("Damaged/poc-a905d63836959293bed720ab7d242bd07b7b7ec81ee3ee1e14e89853344dafbf_min")]
    [InlineData("Damaged/poc-af451a11e18ad9ca6ddc098bfd8b42f803bec2be8fafa6e648b8a7adcfdd0c06_min")]
    [InlineData("Damaged/poc-bd9579c640a6ee867d140c2a4d3bbd6f0452d4726f3f25ed53bf666f558ed245_min")]
    [InlineData("Damaged/poc-c9457552c1c04ea0f98154bc433a5f5d0421a7e705e6f396aba4339568473435_min")]
    [InlineData("Damaged/poc-cbc6ff03d6bc31f0c61476525641852b0220278e6576a651029c50e86f7f0c77")]
    [InlineData("Damaged/poc-d545cd0db4595a1c05504ab53d83cc8c6fce02387545aa49e979ee087c1ddf8f_min")]
    [InlineData("Damaged/poc-df1fefc5fb459cb12183eae34dc696cd0e77b0b8deb4cd1ef3dc25279b7a6bde_min")]
    [InlineData("Damaged/poc-e2106eebb303e685cee66860c931fe1a4eb9af1a7f5bef5b3b95f90c3e8ae0e0_min")]
    [InlineData("Damaged/poc-fb9238840f4d9d93ab3ac989b70000f9795ab6ad399bff00093b944e6a893403_min")]
    public void Fails_gracefully_on_damaged_images(string imagePath)
    {
        string path = Path.Combine(BasePath, imagePath);
        Assert.Throws<EXRFormatException>(() => new EXRFile(path));
    }

    [Theory]
    [InlineData("Damaged/asan_heap-oob_7efd9bd346a5_639_9e0b30ed499cdf9e8802dd64e16a9508_exr")]
    [InlineData("Damaged/asan_heap-oob_7f479f9536bd_391_5953693841a7931caf3d4592f8b9c90b_exr")]
    [InlineData("Damaged/asan_heap-oob_7fb3a7c6fc99_871_52d1f03c515bc91cc894515beea56a4f_exr")]
    [InlineData("Damaged/imf_test_deep_tile_file_fuzz_broken_exr")]
    [InlineData("Damaged/imf_test_tile_file_fuzz_broken_memleak_exr")]
    [InlineData("Damaged/memory_DOS_1")]
    [InlineData("Damaged/memory_DOS_2.1")]
    [InlineData("Damaged/memory_DOS_2.2")]
    [InlineData("Damaged/openexr_2.2.0_memory_allocation_error_2_exr")]
    [InlineData("Damaged/openexr_2.2.0_memory_allocation_error_3_exr")]
    [InlineData("Damaged/signal_sigsegv_7ffff7b21e8a_389_bf048bf41ca71b4e00d2b0edd0a39e27_exr")]
    public void Fails_gracefully_on_invalid_chunk_header_or_validation(string imagePath)
    {
        string path = Path.Combine(BasePath, imagePath);
        using (var file = new EXRFile(path))
        {
            Assert.Throws<EXRFormatException>(() => TestReadParts(file.Parts));
        }
    }

    [Theory]
    [InlineData("Damaged/asan_heap-oob_4cb169_978_5f00ce89c3847e739b256efc49f312cf_exr")]
    [InlineData("Damaged/asan_heap-oob_7f171b7ab3a2_937_b4e2415c399c2ab39548de911223769d_exr")]
    [InlineData("Damaged/asan_heap-oob_7f0faa6bb393_900_7d9ed0a6eaa68f8308a042d725119ad2_exr")]
    [InlineData("Damaged/asan_heap-oob_7f11c0330393_935_240e7cacd61711daf4285366fea95e0c_exr")]
    [InlineData("Damaged/asan_heap-oob_7f1fd65113ac_935_8fd55930e544dc3fb88659a6a8509c14_exr")]
    [InlineData("Damaged/asan_heap-oob_7f4aaebde389_918_a40f029a8121e5e26fe338b1fb91846e_exr")]
    [InlineData("Damaged/asan_heap-oob_7f4d5072b39d_561_5f5e4ef49a581edaf7bf0858fbfcfdd1_exr")]
    [InlineData("Damaged/asan_heap-oob_7f5a5030cdca_702_045a76649e773c9c18b706c9853f18d9_exr")]
    [InlineData("Damaged/asan_heap-oob_7f5cd523238e_878_03c277ec5021331fb301c7c1caa7dfd8_exr")]
    [InlineData("Damaged/asan_heap-oob_7f6798416389_229_18bd946a4fde157b9974d16a51a4851d_exr")]
    [InlineData("Damaged/asan_heap-oob_7f6efa7c53a7_991_4f9bd6fda4f5ae991775244b4945a7fb_exr")]
    [InlineData("Damaged/asan_heap-oob_7f6f881fa398_561_20ecb7f5a431d03a1502c28cab1214ad_exr")]
    [InlineData("Damaged/asan_heap-oob_7f8a69d8339d_829_381ccc69dc6bd21c43a1deb0965bf5ab_exr")]
    [InlineData("Damaged/asan_heap-oob_7fa34eacd389_820_476a8109ebb3f7d02252e773b7bca45d_exr")]
    [InlineData("Damaged/asan_heap-oob_7faf9aba03ac_414_75af58c21b9b9e994747f9d6a5fc46d4_exr")]
    [InlineData("Damaged/asan_heap-oob_7fbe2d8e838e_932_9e9d2b0a870c0ad516189d274c2f98e4_exr")]
    [InlineData("Damaged/asan_heap-oob_7fdb9de0b38e_829_636ff2831c664e14a73282a3299781dd_exr")]
    [InlineData("Damaged/asan_heap-oob_7fe667eb33a2_999_31f64961e47968656f00573f7db5c67d_exr")]
    [InlineData("Damaged/asan_stack-oob_433d4f_436_bb29e6f88ad5f5b2f5f9b68a3655b1d8_exr", Skip = "We don't support DWAA")]
    [InlineData("Damaged/autofuzz_146551958", Skip = "We don't support B44")]
    [InlineData("Damaged/openexr_2.2.0_heap_buffer_overflow_exr")]
    [InlineData("Damaged/poc-4d912f49ddc13ff49f95543880d47c85a8918e563fb723c340264f1719057613.mini")]
    [InlineData("Damaged/poc-66e4d1f68a3112b9aaa93831afbf8e283fd39be5e4591708bad917e6886c0ebb.mini")]
    public void Fails_gracefully_on_invalid_compressed_data(string imagePath)
    {
        string path = Path.Combine(BasePath, imagePath);
        // We disable strict attribute requirements here, because many of the files fail them, but that failure isn't really the point of the test.
        using (var file = new EXRFile(path, new EXRReadOptions { StrictAttributeRequirements = false }))
        {
            Assert.Throws<EXRCompressionException>(() => TestReadParts(file.Parts));
        }
    }
}
