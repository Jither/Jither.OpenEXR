using Jither.OpenEXR.Attributes;
using Jither.OpenEXR.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jither.OpenEXR;

public class TileTests
{
    [Fact]
    public void TileDesc_calculates_correct_mipmap_level_resolutions_down()
    {
        var tileDesc = new TileDesc(15, 17, LevelMode.MipMap, RoundingMode.Down);
        Assert.Collection(tileDesc.Levels,
            level => {
                Assert.Equal(new Dimensions<int>(15, 17), level.Resolution);
                Assert.Equal(new Dimensions<int>(15, 17), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(7, 8), level.Resolution);
                Assert.Equal(new Dimensions<int>(30, 34), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(3, 4), level.Resolution);
                Assert.Equal(new Dimensions<int>(60, 68), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(1, 2), level.Resolution);
                Assert.Equal(new Dimensions<int>(120, 136), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(1, 1), level.Resolution);
                Assert.Equal(new Dimensions<int>(240, 272), level.Coverage);
            }
        );
        // Level count is calculated separately from levels (faster, when only level count is needed).
        // Make sure they match.
        Assert.Equal(tileDesc.LevelCountX, tileDesc.Levels.Count);
    }

    [Fact]
    public void TileDesc_calculates_correct_mipmap_level_resolutions_up()
    {
        var tileDesc = new TileDesc(15, 17, LevelMode.MipMap, RoundingMode.Up);
        Assert.Collection(tileDesc.Levels,
            level => {
                Assert.Equal(new Dimensions<int>(15, 17), level.Resolution);
                Assert.Equal(new Dimensions<int>(15, 17), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(8, 9), level.Resolution);
                Assert.Equal(new Dimensions<int>(30, 34), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(4, 5), level.Resolution);
                Assert.Equal(new Dimensions<int>(60, 68), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(2, 3), level.Resolution);
                Assert.Equal(new Dimensions<int>(120, 136), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(1, 2), level.Resolution);
                Assert.Equal(new Dimensions<int>(240, 272), level.Coverage);
            },
            level => {
                Assert.Equal(new Dimensions<int>(1, 1), level.Resolution);
                Assert.Equal(new Dimensions<int>(480, 544), level.Coverage);
            }
        );
        // Level count is calculated separately from levels (faster, when only level count is needed).
        // Make sure they match.
        Assert.Equal(tileDesc.LevelCountX, tileDesc.Levels.Count);
    }
}
