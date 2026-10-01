using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Tests.Geometry;

public class PixelRectTests
{
    [Fact]
    public void Inflate_grows_each_side()
    {
        Assert.Equal(new PixelRect(5, 8, 30, 24), new PixelRect(10, 10, 20, 20).Inflate(5, 2));
    }

    [Fact]
    public void Intersect_clips_to_the_overlap()
    {
        var result = new PixelRect(0, 0, 100, 100).Intersect(new PixelRect(80, -10, 50, 50));

        Assert.Equal(new PixelRect(80, 0, 20, 40), result);
    }

    [Fact]
    public void Intersect_without_overlap_is_empty()
    {
        Assert.True(new PixelRect(0, 0, 10, 10).Intersect(new PixelRect(20, 20, 5, 5)).IsEmpty);
    }

    [Fact]
    public void Contains_excludes_right_and_bottom_edges()
    {
        var rect = new PixelRect(0, 0, 10, 10);

        Assert.True(rect.Contains(new PixelPoint(9, 9)));
        Assert.False(rect.Contains(new PixelPoint(10, 5)));
    }
}
