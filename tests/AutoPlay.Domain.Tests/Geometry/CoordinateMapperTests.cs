using AutoPlay.Domain.Geometry;

namespace AutoPlay.Domain.Tests.Geometry;

public class CoordinateMapperTests
{
    private static readonly PixelRect Region = new(100, 50, 1280, 720);

    [Fact]
    public void Point_round_trips_through_normalized_coordinates()
    {
        var point = new PixelPoint(740, 410);

        var normalized = CoordinateMapper.ToNormalized(point, Region);

        Assert.Equal(point, CoordinateMapper.ToPixel(normalized, Region));
    }

    [Fact]
    public void Point_is_normalized_from_the_pixel_center()
    {
        var normalized = CoordinateMapper.ToNormalized(new PixelPoint(100, 50), new PixelRect(100, 50, 10, 4));

        Assert.Equal(new NormalizedPoint(0.05, 0.125), normalized);
    }

    [Fact]
    public void Point_scales_proportionally_and_stays_inside_the_region()
    {
        var region = new PixelRect(0, 0, 100, 100);

        Assert.Equal(new PixelPoint(49, 49), CoordinateMapper.ToPixel(new NormalizedPoint(0.495, 0.495), region));
        Assert.Equal(new PixelPoint(99, 99), CoordinateMapper.ToPixel(new NormalizedPoint(1, 1), region));
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
