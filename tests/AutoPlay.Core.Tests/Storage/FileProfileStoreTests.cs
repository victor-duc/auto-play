using AutoPlay.Core.Geometry;
using AutoPlay.Core.Imaging;
using AutoPlay.Core.Model;
using AutoPlay.Core.Storage;
using AutoPlay.Core.Tests.Fakes;

namespace AutoPlay.Core.Tests.Storage;

public sealed class FileProfileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AutoPlayTests", Guid.NewGuid().ToString());
    private readonly FileProfileStore _store;

    public FileProfileStoreTests() => _store = new FileProfileStore(_root, new FakeImageCodec());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Missing_root_folder_means_no_profiles()
    {
        Assert.Empty(_store.LoadProfiles());
    }

    [Fact]
    public void Profile_round_trips()
    {
        var profile = new Profile
        {
            Name = "Hero Wars",
            LastTargetRegion = new PixelRect(120, 95, 1280, 720),
            Defaults = { MatchThreshold = 0.75 },
        };

        _store.SaveProfile(profile);
        var loaded = Assert.Single(_store.LoadProfiles());

        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal("Hero Wars", loaded.Name);
        Assert.Equal(profile.LastTargetRegion, loaded.LastTargetRegion);
        Assert.Equal(0.75, loaded.Defaults.MatchThreshold);
    }

    [Fact]
    public void Screen_round_trips_with_its_images()
    {
        var profile = new Profile { Name = "Game" };
        _store.SaveProfile(profile);
        var location = new Location
        {
            Name = "Raid button",
            Bounds = new NormalizedRect(0.7, 0.8, 0.1, 0.05),
            ClickPoint = new NormalizedPoint(0.75, 0.825),
            MatchThreshold = 0.9,
        };
        var screen = new Screen
        {
            Name = "Campaign map",
            RecordedRegionSize = new PixelSize(1280, 720),
            DefaultDelay = new DelayRange(500, 900),
            Locations = [location],
        };
        var capture = TestImages.Gradient(16, 9);
        var template = TestImages.Gradient(4, 3);

        _store.SaveScreen(profile.Id, screen, capture, new Dictionary<Guid, RawImage> { [location.Id] = template });

        var loaded = Assert.Single(_store.LoadScreens(profile.Id));
        Assert.Equal("Campaign map", loaded.Name);
        Assert.Equal(new DelayRange(500, 900), loaded.DefaultDelay);
        var loadedLocation = Assert.Single(loaded.Locations);
        Assert.Equal(location.Bounds, loadedLocation.Bounds);
        Assert.Equal(location.ClickPoint, loadedLocation.ClickPoint);
        Assert.Equal(PositioningMode.Proportional, loadedLocation.Positioning);
        Assert.Equal(capture.Pixels, _store.LoadScreenCapture(profile.Id, screen.Id).Pixels);
        Assert.Equal(template.Pixels, _store.LoadLocationTemplate(profile.Id, screen.Id, location.Id).Pixels);
    }

    [Fact]
    public void Saving_a_screen_requires_a_template_for_every_location()
    {
        var screen = new Screen { Name = "S", Locations = [new Location { Name = "L" }] };

        Assert.Throws<ArgumentException>(() =>
            _store.SaveScreen(Guid.NewGuid(), screen, TestImages.Solid(2, 2), new Dictionary<Guid, RawImage>()));
    }

    [Fact]
    public void Sequence_round_trips_and_can_be_deleted()
    {
        var profileId = Guid.NewGuid();
        var sequence = new Sequence
        {
            Name = "Daily raids",
            RepeatCount = 3,
            Steps =
            [
                new SequenceStep { ScreenId = Guid.NewGuid(), LocationId = Guid.NewGuid() },
                new SequenceStep { ScreenId = Guid.NewGuid(), LocationId = Guid.NewGuid(), Delay = new DelayRange(0, 10), Verify = false },
            ],
        };

        _store.SaveSequence(profileId, sequence);
        var loaded = Assert.Single(_store.LoadSequences(profileId));

        Assert.Equal(3, loaded.RepeatCount);
        Assert.Equal(2, loaded.Steps.Count);
        Assert.Null(loaded.Steps[0].Delay);
        Assert.True(loaded.Steps[0].Verify);
        Assert.Equal(new DelayRange(0, 10), loaded.Steps[1].Delay);
        Assert.False(loaded.Steps[1].Verify);

        _store.DeleteSequence(profileId, sequence.Id);
        Assert.Empty(_store.LoadSequences(profileId));
    }

    [Fact]
    public void Deleting_a_profile_removes_its_folder()
    {
        var profile = new Profile { Name = "Temp" };
        _store.SaveProfile(profile);

        _store.DeleteProfile(profile.Id);

        Assert.Empty(_store.LoadProfiles());
    }
}
