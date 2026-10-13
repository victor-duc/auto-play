using AutoPlay.Application.Ports;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;
using AutoPlay.TestDoubles;

namespace AutoPlay.Application.Tests.UseCases;

public class CaptureInboxServiceTests
{
    private readonly InMemoryRepository _repository = new();
    private readonly FakeScreenCapture _screenCapture = new();
    private readonly FakeClock _clock = new();
    private readonly CaptureInboxService _service;

    public CaptureInboxServiceTests() => _service = new CaptureInboxService(_repository, _screenCapture, _clock);

    [Fact]
    public void QuickCapture_captures_the_last_target_region()
    {
        var profile = new Profile { Name = "Game", LastTargetRegion = new PixelRect(10, 20, 300, 200) };

        var result = _service.QuickCapture(profile);

        Assert.True(result.Succeeded);
        Assert.Equal(new PixelRect(10, 20, 300, 200), Assert.Single(_screenCapture.Captures));
        Assert.Equal(_clock.UtcNow, result.Value!.CapturedAt);
        var stored = Assert.Single(_repository.Inbox[profile.Id]);
        Assert.Equal(300, stored.Image.Width);
    }

    [Fact]
    public void QuickCapture_needs_a_known_target_region()
    {
        var result = _service.QuickCapture(new Profile { Name = "Game" });

        Assert.False(result.Succeeded);
        Assert.Contains("target area is not known", result.Errors[0]);
        Assert.Empty(_screenCapture.Captures);
    }

    [Fact]
    public void QuickCapture_reports_storage_errors()
    {
        _repository.Failure = new PersistenceException("Disk full");

        var result = _service.QuickCapture(new Profile { Name = "Game", LastTargetRegion = new PixelRect(0, 0, 10, 10) });

        Assert.Equal(["Disk full"], result.Errors);
    }

    [Fact]
    public void Pending_captures_are_listed_oldest_first_and_can_be_discarded()
    {
        var profileId = Guid.NewGuid();
        var newer = new PendingCapture(Guid.NewGuid(), _clock.UtcNow.AddMinutes(5));
        var older = new PendingCapture(Guid.NewGuid(), _clock.UtcNow);
        _repository.SavePendingCapture(profileId, newer, TestImages.Solid(2, 2));
        _repository.SavePendingCapture(profileId, older, TestImages.Solid(2, 2));

        Assert.Equal([older, newer], _service.GetPendingCaptures(profileId));

        Assert.True(_service.Discard(profileId, older.Id).Succeeded);

        Assert.Equal([newer], _service.GetPendingCaptures(profileId));
    }
}
