using AutoPlay.Core.Geometry;
using AutoPlay.Core.Model;
using AutoPlay.Core.Recording;
using AutoPlay.Core.Tests.Fakes;

namespace AutoPlay.Core.Tests.Recording;

public class ScreenBuilderTests
{
    private static readonly PixelSize CaptureSize = new(200, 100);

    private static LocationDraft Draft(string name, int x = 10, int y = 10) => new()
    {
        Name = name,
        Bounds = new PixelRect(x, y, 20, 10),
        ClickPoint = new PixelPoint(x + 10, y + 5),
    };

    [Fact]
    public void Valid_screen_has_no_errors()
    {
        var errors = ScreenBuilder.Validate("Map", [Draft("A"), Draft("B", 50)], CaptureSize, ["Home"]);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Screen_name_is_required(string name)
    {
        Assert.Contains("The screen name is required.", ScreenBuilder.Validate(name, [], CaptureSize, []));
    }

    [Fact]
    public void Screen_name_must_be_unique_ignoring_case_and_spaces()
    {
        var errors = ScreenBuilder.Validate(" map ", [], CaptureSize, ["Map"]);

        Assert.Contains("Another screen is already named 'map'.", errors);
    }

    [Fact]
    public void Location_names_are_required_and_unique()
    {
        var errors = ScreenBuilder.Validate("Map", [Draft(""), Draft("Raid"), Draft("raid ", 50)], CaptureSize, []);

        Assert.Contains("Every location needs a name.", errors);
        Assert.Contains("Several locations are named 'Raid'.", errors);
    }

    [Fact]
    public void Click_point_must_lie_within_the_rectangle()
    {
        var draft = Draft("A") with { ClickPoint = new PixelPoint(0, 0) };

        Assert.Contains("The click point of 'A' must lie within its rectangle.", ScreenBuilder.Validate("Map", [draft], CaptureSize, []));
    }

    [Fact]
    public void Rectangle_must_lie_within_the_capture()
    {
        var draft = Draft("A", x: 190);

        Assert.Contains("The rectangle of 'A' must lie within the capture.", ScreenBuilder.Validate("Map", [draft], CaptureSize, []));
    }

    [Fact]
    public void Match_threshold_must_be_between_0_and_1()
    {
        var draft = Draft("A") with { MatchThreshold = 1.5 };

        Assert.Contains("The match threshold of 'A' must be between 0 and 1.", ScreenBuilder.Validate("Map", [draft], CaptureSize, []));
    }

    [Fact]
    public void Build_normalizes_locations_and_crops_templates()
    {
        var capture = TestImages.Gradient(200, 100);
        var draft = Draft(" Raid ", 50, 20) with { MatchThreshold = 0.9 };
        var screenId = Guid.NewGuid();

        var (screen, templates) = ScreenBuilder.Build(screenId, " Map ", new DelayRange(100, 200), capture, [draft]);

        Assert.Equal(screenId, screen.Id);
        Assert.Equal("Map", screen.Name);
        Assert.Equal(CaptureSize, screen.RecordedRegionSize);
        Assert.Equal(new DelayRange(100, 200), screen.DefaultDelay);
        var location = Assert.Single(screen.Locations);
        Assert.Equal(draft.Id, location.Id);
        Assert.Equal("Raid", location.Name);
        Assert.Equal(new NormalizedRect(0.25, 0.2, 0.1, 0.1), location.Bounds);
        Assert.Equal(0.9, location.MatchThreshold);
        var template = templates[draft.Id];
        Assert.Equal(new PixelSize(20, 10), template.Size);
        Assert.Equal(50, template.Pixels[0]); // x of the top-left pixel in the gradient
        Assert.Equal(20, template.Pixels[1]); // y of the top-left pixel in the gradient
    }

    [Fact]
    public void ToDrafts_round_trips_with_Build()
    {
        var capture = TestImages.Gradient(197, 83); // Odd sizes to exercise rounding.
        LocationDraft[] drafts =
        [
            new() { Name = "A", Bounds = new PixelRect(13, 7, 31, 17), ClickPoint = new PixelPoint(14, 23) },
            new() { Name = "B", Bounds = new PixelRect(150, 60, 47, 23), ClickPoint = new PixelPoint(196, 82), MatchThreshold = 0.7 },
        ];

        var (screen, _) = ScreenBuilder.Build(Guid.NewGuid(), "S", new DelayRange(0, 0), capture, drafts);
        var restored = ScreenBuilder.ToDrafts(screen, capture.Size);

        Assert.Equal(drafts, restored);
    }
}
