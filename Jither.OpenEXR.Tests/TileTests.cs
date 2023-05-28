using Jither.OpenEXR.Attributes;
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
        Assert.Collection(tileDesc.Resolutions,
            level => { Assert.Equal(15, level.X); Assert.Equal(17, level.Y); },
            level => { Assert.Equal( 7, level.X); Assert.Equal( 8, level.Y); },
            level => { Assert.Equal( 3, level.X); Assert.Equal( 4, level.Y); },
            level => { Assert.Equal( 1, level.X); Assert.Equal( 2, level.Y); },
            level => { Assert.Equal( 1, level.X); Assert.Equal( 1, level.Y); }
        );
        Assert.Collection(tileDesc.Coverages,
            level => { Assert.Equal( 15, level.X); Assert.Equal( 17, level.Y); },
            level => { Assert.Equal( 30, level.X); Assert.Equal( 34, level.Y); },
            level => { Assert.Equal( 60, level.X); Assert.Equal( 68, level.Y); },
            level => { Assert.Equal(120, level.X); Assert.Equal(136, level.Y); },
            level => { Assert.Equal(240, level.X); Assert.Equal(272, level.Y); }
        );
    }

    [Fact]
    public void TileDesc_calculates_correct_mipmap_level_resolutions_up()
    {
        var tileDesc = new TileDesc(15, 17, LevelMode.MipMap, RoundingMode.Up);
        Assert.Collection(tileDesc.Resolutions,
            level => { Assert.Equal(15, level.X); Assert.Equal(17, level.Y); },
            level => { Assert.Equal( 8, level.X); Assert.Equal( 9, level.Y); },
            level => { Assert.Equal( 4, level.X); Assert.Equal( 5, level.Y); },
            level => { Assert.Equal( 2, level.X); Assert.Equal( 3, level.Y); },
            level => { Assert.Equal( 1, level.X); Assert.Equal( 2, level.Y); },
            level => { Assert.Equal( 1, level.X); Assert.Equal( 1, level.Y); }
        );
        Assert.Collection(tileDesc.Coverages,
            level => { Assert.Equal( 15, level.X); Assert.Equal( 17, level.Y); },
            level => { Assert.Equal( 30, level.X); Assert.Equal( 34, level.Y); },
            level => { Assert.Equal( 60, level.X); Assert.Equal( 68, level.Y); },
            level => { Assert.Equal(120, level.X); Assert.Equal(136, level.Y); },
            level => { Assert.Equal(240, level.X); Assert.Equal(272, level.Y); },
            level => { Assert.Equal(480, level.X); Assert.Equal(544, level.Y); }
        );
    }
}
