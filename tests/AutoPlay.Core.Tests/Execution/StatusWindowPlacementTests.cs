using AutoPlay.Core.Execution;
using AutoPlay.Domain.Geometry;

namespace AutoPlay.Core.Tests.Execution;

public class StatusWindowPlacementTests
{
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);
    private static readonly PixelSize Window = new(360, 200);

    [Fact]
    public void Goes_right_of_the_region_when_there_is_room()
    {
        var placed = StatusWindowPlacement.Place(Window, new PixelRect(100, 100, 1280, 720), WorkArea);

        Assert.Equal(new PixelRect(1388, 100, 360, 200), placed);
    }

    [Fact]
    public void Goes_left_when_the_right_side_is_too_narrow()
    {
        var placed = StatusWindowPlacement.Place(Window, new PixelRect(500, 100, 1300, 720), WorkArea);

        Assert.Equal(new PixelRect(132, 100, 360, 200), placed);
    }

    [Fact]
    public void Goes_below_when_the_region_is_as_wide_as_the_screen()
    {
        var placed = StatusWindowPlacement.Place(Window, new PixelRect(0, 0, 1920, 700), WorkArea);

        Assert.Equal(new PixelRect(1560, 708, 360, 200), placed);
    }

    [Fact]
    public void Slides_to_stay_inside_the_work_area()
    {
        var placed = StatusWindowPlacement.Place(Window, new PixelRect(100, 950, 1280, 80), WorkArea);

        Assert.Equal(new PixelRect(1388, 840, 360, 200), placed);
    }

    [Fact]
    public void Uses_the_bottom_right_corner_when_the_region_fills_the_screen()
    {
        var placed = StatusWindowPlacement.Place(Window, WorkArea, WorkArea);

        Assert.Equal(new PixelRect(1552, 832, 360, 200), placed);
    }

    [Fact]
    public void Works_on_a_secondary_monitor()
    {
        var secondary = new PixelRect(1920, 0, 1920, 1040);

        var placed = StatusWindowPlacement.Place(Window, new PixelRect(2000, 100, 1280, 720), secondary);

        Assert.Equal(new PixelRect(3288, 100, 360, 200), placed);
    }
}
