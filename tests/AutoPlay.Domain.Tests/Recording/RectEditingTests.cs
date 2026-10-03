using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Recording;

namespace AutoPlay.Domain.Tests.Recording;

public class RectEditingTests
{
    private static readonly PixelSize Bounds = new(200, 100);

    [Fact]
    public void FromCorners_works_in_any_drag_direction()
    {
        Assert.Equal(new PixelRect(10, 20, 30, 40), RectEditing.FromCorners(new PixelPoint(40, 60), new PixelPoint(10, 20)));
    }

    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(4, 0, false)]
    [InlineData(0, -4, false)]
    public void IsClick_tolerates_small_moves(int dx, int dy, bool expected)
    {
        Assert.Equal(expected, RectEditing.IsClick(new PixelPoint(50, 50), new PixelPoint(50 + dx, 50 + dy)));
    }

    [Fact]
    public void CenteredAt_is_kept_inside_the_capture()
    {
        var rect = RectEditing.CenteredAt(new PixelPoint(5, 95), new PixelSize(48, 48), Bounds);

        Assert.Equal(new PixelRect(0, 52, 48, 48), rect);
    }

    [Fact]
    public void KeepInside_shrinks_only_when_larger_than_the_capture()
    {
        Assert.Equal(new PixelRect(150, 0, 50, 100), RectEditing.KeepInside(new PixelRect(180, -10, 50, 300), Bounds));
    }

    [Fact]
    public void Normalize_clips_and_enforces_the_minimum_size()
    {
        Assert.Equal(new PixelRect(195 - 3, 10, 8, 8), RectEditing.Normalize(new PixelRect(195, 10, 20, 2), Bounds));
    }

    [Theory]
    [InlineData(10, 10, RectHandle.TopLeft)]
    [InlineData(52, 8, RectHandle.TopRight)]
    [InlineData(12, 50, RectHandle.BottomLeft)]
    [InlineData(50, 50, RectHandle.BottomRight)]
    [InlineData(30, 30, RectHandle.Body)]
    [InlineData(80, 30, RectHandle.None)]
    public void HitTest_finds_corners_then_body(int x, int y, RectHandle expected)
    {
        Assert.Equal(expected, RectEditing.HitTest(new PixelRect(10, 10, 40, 40), new PixelPoint(x, y), tolerance: 3));
    }

    [Fact]
    public void Resize_moves_only_the_grabbed_corner()
    {
        var rect = RectEditing.Resize(new PixelRect(10, 10, 40, 40), RectHandle.TopLeft, new PixelPoint(0, 5), Bounds);

        Assert.Equal(new PixelRect(0, 5, 50, 45), rect);
    }

    [Fact]
    public void Resize_cannot_invert_or_go_below_the_minimum_size()
    {
        var rect = RectEditing.Resize(new PixelRect(10, 10, 40, 40), RectHandle.BottomRight, new PixelPoint(0, 0), Bounds);

        Assert.Equal(new PixelRect(10, 10, RectEditing.MinimumSize, RectEditing.MinimumSize), rect);
    }

    [Fact]
    public void Resize_stays_inside_the_capture()
    {
        var rect = RectEditing.Resize(new PixelRect(150, 50, 40, 40), RectHandle.BottomRight, new PixelPoint(500, 500), Bounds);

        Assert.Equal(new PixelRect(150, 50, 50, 50), rect);
    }

    [Fact]
    public void Clamp_keeps_a_point_inside_the_rectangle()
    {
        Assert.Equal(new PixelPoint(49, 10), RectEditing.Clamp(new PixelPoint(90, 0), new PixelRect(10, 10, 40, 40)));
    }
}
