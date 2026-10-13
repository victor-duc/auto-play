using AutoPlay.Application.Ports;
using AutoPlay.TestDoubles;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Recording;

namespace AutoPlay.Application.Tests.UseCases;

public class ScreenServiceTests
{
    private readonly Guid _profileId = Guid.NewGuid();
    private readonly InMemoryRepository _repository = new();
    private readonly FakeScreenCapture _capture = new();
    private readonly FakeTemplateMatcher _matcher = new(new MatchResult(default, 0));
    private readonly ScreenService _service;

    public ScreenServiceTests() => _service = new ScreenService(_repository, _repository, _repository, _capture, _matcher);

    private static ScreenDraft Draft(string name, Guid? id = null) => new(
        id ?? Guid.NewGuid(),
        name,
        new DelayRange(100, 200),
        TestImages.Gradient(100, 50),
        [new LocationDraft { Name = "Button", Bounds = new PixelRect(10, 10, 20, 10), ClickPoint = new PixelPoint(20, 15) }]);

    [Fact]
    public void Capture_captures_the_region_and_remembers_it()
    {
        var profile = new Profile { Name = "Game" };
        var region = new PixelRect(5, 5, 40, 30);

        var image = _service.Capture(profile, region);

        Assert.Equal(new PixelSize(40, 30), image.Size);
        Assert.Equal([region], _capture.Captures);
        Assert.Equal(region, Assert.Single(_repository.Profiles).LastTargetRegion);
    }

    [Fact]
    public void Recapture_repositions_the_locations_found_and_flags_the_others()
    {
        var profile = new Profile { Name = "Game" };
        var moved = new Location { Name = "Moved", Bounds = new NormalizedRect(0.1, 0.1, 0.2, 0.2), ClickPoint = new NormalizedPoint(0.15, 0.15) };
        var lost = new Location { Name = "Lost", Bounds = new NormalizedRect(0.6, 0.6, 0.2, 0.2), ClickPoint = new NormalizedPoint(0.7, 0.7) };
        var screen = new Screen { Name = "Map", RecordedRegionSize = new PixelSize(100, 50), Locations = [moved, lost] };
        _repository.SaveScreen(_profileId, screen, TestImages.Solid(100, 50), new Dictionary<Guid, RawImage>
        {
            [moved.Id] = TestImages.Solid(20, 10),
            [lost.Id] = TestImages.Solid(20, 10),
        });
        profile.Id = _profileId;

        // New capture 200 x 100: 'Moved' is expected at (20, 10, 40, 20); its search area starts at (0, 0).
        var matcher = new FakeTemplateMatcher(
            new MatchResult(new PixelRect(30, 15, 40, 20), 0.95),
            new MatchResult(default, 0.1),
            new MatchResult(default, 0.2));
        var service = new ScreenService(_repository, _repository, _repository, _capture, matcher);

        var result = service.Recapture(profile, screen, new PixelRect(0, 0, 200, 100));

        Assert.Equal(new PixelSize(200, 100), result.Capture.Size);
        Assert.Equal([lost.Id], result.NotFound);
        var movedDraft = result.Locations.Single(l => l.Id == moved.Id);
        Assert.Equal(new PixelRect(30, 15, 40, 20), movedDraft.Bounds);
        Assert.Equal(new PixelPoint(40, 20), movedDraft.ClickPoint);
        Assert.Equal(new PixelRect(120, 60, 40, 20), result.Locations.Single(l => l.Id == lost.Id).Bounds);
        Assert.Equal(3, matcher.RequestedSizes.Count); // Nearby for both, then the whole capture for the lost one.
        Assert.Equal(new PixelRect(0, 0, 200, 100), Assert.Single(_repository.Profiles).LastTargetRegion);
    }

    [Fact]
    public void SaveScreen_stores_the_screen_its_capture_and_templates()
    {
        var draft = Draft(" Map ");

        var result = _service.SaveScreen(_profileId, draft);

        Assert.True(result.Succeeded);
        var screen = Assert.Single(_service.GetScreens(_profileId));
        Assert.Equal("Map", screen.Name);
        Assert.Same(draft.Capture, _service.GetCapture(_profileId, screen.Id));
        var location = Assert.Single(screen.Locations);
        Assert.Equal(new PixelSize(20, 10), _service.GetTemplate(_profileId, screen.Id, location.Id).Size);
    }

    [Fact]
    public void SaveScreen_rejects_the_name_of_another_screen()
    {
        _service.SaveScreen(_profileId, Draft("Map"));

        var result = _service.SaveScreen(_profileId, Draft("map"));

        Assert.Equal(["Another screen is already named 'map'."], result.Errors);
        Assert.Single(_service.GetScreens(_profileId));
    }

    [Fact]
    public void SaveScreen_accepts_the_same_name_when_editing_the_screen()
    {
        var id = Guid.NewGuid();
        _service.SaveScreen(_profileId, Draft("Map", id));

        var result = _service.SaveScreen(_profileId, Draft("Map", id));

        Assert.True(result.Succeeded);
        Assert.Single(_service.GetScreens(_profileId));
    }

    [Fact]
    public void SaveScreen_reports_storage_errors()
    {
        _repository.Failure = new PersistenceException("Access denied");

        Assert.Equal(["Access denied"], _service.SaveScreen(_profileId, Draft("Map")).Errors);
    }

    [Fact]
    public void GetUsages_and_DeleteScreen()
    {
        _service.SaveScreen(_profileId, Draft("Map"));
        var screen = _service.GetScreens(_profileId)[0];
        var user = new Sequence { Name = "Raids", Steps = [new SequenceStep { ScreenId = screen.Id, LocationId = screen.Locations[0].Id }] };
        _repository.SaveSequence(_profileId, user);
        _repository.SaveSequence(_profileId, new Sequence { Name = "Other" });

        Assert.Equal([user], _service.GetUsages(_profileId, screen.Id));

        _service.DeleteScreen(_profileId, screen.Id);
        Assert.Empty(_service.GetScreens(_profileId));
    }

    [Fact]
    public void TestLocation_looks_for_the_location_once_in_its_search_area()
    {
        var location = new Location { Name = "Attack", Bounds = new NormalizedRect(0.1, 0.1, 0.2, 0.2), ClickPoint = new NormalizedPoint(0.2, 0.2) };
        var screen = new Screen { Name = "Map", RecordedRegionSize = new PixelSize(200, 100), Locations = [location] };
        _repository.SaveScreen(_profileId, screen, TestImages.Solid(200, 100), new Dictionary<Guid, RawImage> { [location.Id] = TestImages.Solid(40, 20) });
        var profile = new Profile { Id = _profileId, Name = "Game", LastTargetRegion = new PixelRect(100, 100, 200, 100) };
        var matcher = new FakeTemplateMatcher(new MatchResult(new PixelRect(25, 12, 40, 20), 0.9));
        var service = new ScreenService(_repository, _repository, _repository, _capture, matcher);

        var result = service.TestLocation(profile, screen.Id, location.Id);

        // Expected at (120, 110, 40, 20); with the default 50% margin the search area is (100, 100, 80, 40).
        Assert.Equal([new PixelRect(100, 100, 80, 40)], _capture.Captures);
        Assert.Equal(new LocationTestResult("Map", "Attack", 0.9, 0.8, 5, 2), result.Value);
        Assert.True(result.Value!.Found);
    }

    [Fact]
    public void TestLocation_needs_a_known_target_region_and_an_existing_location()
    {
        var noRegion = _service.TestLocation(new Profile { Name = "Game" }, Guid.NewGuid(), Guid.NewGuid());
        var missing = _service.TestLocation(
            new Profile { Id = _profileId, Name = "Game", LastTargetRegion = new PixelRect(0, 0, 10, 10) }, Guid.NewGuid(), Guid.NewGuid());

        Assert.Contains("target area is not known", noRegion.Errors[0]);
        Assert.Contains("does not exist", missing.Errors[0]);
        Assert.Empty(_capture.Captures);
    }
}
