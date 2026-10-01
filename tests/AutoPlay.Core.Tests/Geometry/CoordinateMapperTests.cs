using AutoPlay.Core.Geometry;

namespace AutoPlay.Core.Tests.Geometry;

public class CoordinateMapperTests
{
    private static readonly PixelRect Region = new(100, 50, 1280, 720);

    [Fact]
    public void Point_round_trips_through_normalized_coordinates()
    {
        var point = new PixelPoint(740, 410);

        var normalized = CoordinateMapper.ToNormalized(point, Region);

        Assert.Equal(new NormalizedPoint(0.5, 0.5), normalized);
        Assert.Equal(point, CoordinateMapper.ToPixel(normalized, Region));
    }

    [Fact]
    public void Rect_scales_proportionally_with_the_region()
    {
        var recorded = new PixelRect(100 + 640, 50 + 360, 128, 72);
        var normalized = CoordinateMapper.ToNormalized(recorded, Region);

        var resized = CoordinateMapper.ToPixel(normalized, new PixelRect(0, 0, 640, 360));

        Assert.Equal(new PixelRect(320, 180, 64, 36), resized);
    }

    [Fact]
    public void Rect_is_never_smaller_than_one_pixel()
    {
        var rect = CoordinateMapper.ToPixel(new NormalizedRect(0.5, 0.5, 0.0001, 0.0001), Region);

        Assert.Equal(1, rect.Width);
        Assert.Equal(1, rect.Height);
    }

    [Fact]
    public void Empty_region_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CoordinateMapper.ToNormalized(new PixelPoint(1, 1), default));
    }
}
