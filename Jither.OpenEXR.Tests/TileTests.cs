using Jither.OpenEXR.Attributes;
using Jither.OpenEXR.Drawing;

namespace Jither.OpenEXR;

public class TileTests
{
    [Fact]
    public void TileDesc_calculates_correct_mipmap_level_resolutions_down()
    {
        var tileDesc = new TileDesc(16, 16, LevelMode.MipMap, RoundingMode.Down);
        var tileInfo = tileDesc.GetTilingInformation(15, 17);
        Assert.Collection(tileInfo.Levels,
            level => {
                Assert.Equal(15, level.Width);
                Assert.Equal(17, level.Height);
            },
            level => {
                Assert.Equal(7, level.Width);
                Assert.Equal(8, level.Height);
            },
            level => {
                Assert.Equal(3, level.Width);
                Assert.Equal(4, level.Height);
            },
            level => {
                Assert.Equal(1, level.Width);
                Assert.Equal(2, level.Height);
            },
            level => {
                Assert.Equal(1, level.Width);
                Assert.Equal(1, level.Height);
            }
        );
    }

    [Fact]
    public void TileDesc_calculates_correct_mipmap_level_resolutions_up()
    {
        var tileDesc = new TileDesc(16, 16, LevelMode.MipMap, RoundingMode.Up);
        var tileInfo = tileDesc.GetTilingInformation(15, 17);
        Assert.Collection(tileInfo.Levels,
            level => {
                Assert.Equal(15, level.Width);
                Assert.Equal(17, level.Height);
            },
            level => {
                Assert.Equal(8, level.Width);
                Assert.Equal(9, level.Height);
            },
            level => {
                Assert.Equal(4, level.Width);
                Assert.Equal(5, level.Height);
            },
            level => {
                Assert.Equal(2, level.Width);
                Assert.Equal(3, level.Height);
            },
            level => {
                Assert.Equal(1, level.Width);
                Assert.Equal(2, level.Height);
            },
            level => {
                Assert.Equal(1, level.Width);
                Assert.Equal(1, level.Height);
            }
        );
    }

    [Fact]
    public void TilingInformation_has_correct_information()
    {
        var tileDesc = new TileDesc(16, 16, LevelMode.MipMap, RoundingMode.Up);
        var tilingInfo = tileDesc.GetTilingInformation(315, 257);
        Assert.Equal(10, tilingInfo.LevelXCount);
        Assert.Equal(10, tilingInfo.LevelYCount);
        Assert.Equal(10, tilingInfo.Levels.Count);
        Assert.Equal(20 * 17 + 10 * 9 + 5 * 5 + 3 * 3 + 2 * 2 + 1 * 1 + 1 * 1 + 1 * 1 + 1 * 1 + 1 * 1, tilingInfo.TotalChunkCount);
    }
}
