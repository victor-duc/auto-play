using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Recording;
using AutoPlay.Presentation.ViewModels;

namespace AutoPlay.Presentation.Tests;

public class LocationViewModelTests
{
    private static LocationViewModel Centered() => new(new LocationDraft
    {
        Name = "A",
        Bounds = new PixelRect(10, 10, 20, 20),
        ClickPoint = new PixelPoint(20, 20),
    });

    [Fact]
    public void Centered_click_point_follows_moves_and_resizes()
    {
        var location = Centered();

        location.MoveTo(new PixelRect(30, 30, 20, 20));
        Assert.Equal(new PixelPoint(40, 40), location.ClickPoint);

        location.ResizeTo(new PixelRect(30, 30, 40, 10));
        Assert.Equal(new PixelPoint(50, 35), location.ClickPoint);
    }

    [Fact]
    public void Explicit_click_point_keeps_its_relative_position()
    {
        var location = Centered();
        location.SetClickPoint(new PixelPoint(12, 14));

        location.MoveTo(new PixelRect(110, 10, 20, 20));

        Assert.Equal(new PixelPoint(112, 14), location.ClickPoint);
        Assert.False(location.ClickPointIsCenter);
    }

    [Fact]
    public void SetClickPoint_is_kept_inside_the_rectangle()
    {
        var location = Centered();

        location.SetClickPoint(new PixelPoint(100, 0));

        Assert.Equal(new PixelPoint(29, 10), location.ClickPoint);
    }

    [Fact]
    public void RedrawTo_keeps_the_relative_click_position()
    {
        var location = Centered();
        location.SetClickPoint(new PixelPoint(10, 29));

        location.RedrawTo(new PixelRect(100, 100, 40, 40));

        // Mapped from the pixel center: the first and last of 20 columns become columns 1 and 39 of 40.
        Assert.Equal(new PixelPoint(101, 139), location.ClickPoint);
    }

    [Fact]
    public void Editing_clears_the_review_flag()
    {
        var location = Centered();
        location.NeedsReview = true;

        location.RedrawTo(new PixelRect(0, 0, 10, 10));

        Assert.False(location.NeedsReview);
        Assert.Equal(new PixelPoint(5, 5), location.ClickPoint);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("0.9", 0.9)]
    [InlineData(" 0,75 ", 0.75)]
    public void Threshold_accepts_dot_and_comma(string text, double? expected)
    {
        var location = Centered();
        location.MatchThresholdText = text;

        Assert.Null(location.TryToDraft(out var draft));
        Assert.Equal(expected, draft.MatchThreshold);
    }
}
