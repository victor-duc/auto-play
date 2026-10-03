using AutoPlay.Core.Abstractions;
using AutoPlay.Core.Execution;
using AutoPlay.Core.Geometry;
using AutoPlay.Core.Imaging;
using AutoPlay.Core.Model;
using AutoPlay.Core.Tests.Fakes;

namespace AutoPlay.Core.Tests.Execution;

public class SequenceRunnerTests
{
    // Target region 1000x500 at (100, 100). The location covers 100x50 px at (500, 300) on screen.
    private static readonly PixelRect Region = new(100, 100, 1000, 500);

    private readonly Profile _profile = new() { Name = "Test" };
    private readonly Location _location = new()
    {
        Name = "Button",
        Bounds = new NormalizedRect(0.4, 0.4, 0.1, 0.1),
        ClickPoint = new NormalizedPoint(0.45, 0.45),
    };

    private readonly Screen _screen;
    private readonly FakeScreenCapture _capture = new();
    private readonly FakeInputDriver _input = new();
    private readonly FakePowerManager _power = new();
    private readonly FakeClock _clock = new();

    public SequenceRunnerTests()
    {
        _screen = new Screen
        {
            Name = "Main",
            RecordedRegionSize = new PixelSize(1000, 500),
            DefaultDelay = new DelayRange(100, 100),
            Locations = [_location],
        };
    }

    // The search area is the location (500,300 100x50) inflated by 50% on each side: (450, 275, 200, 100).
    // A match at (50, 25) inside it means the element is exactly where expected.
    private static MatchResult MatchAtExpectedPosition(double score = 0.95) => new(new PixelRect(50, 25, 100, 50), score);

    [Fact]
    public async Task Clicks_the_location_when_it_is_found()
    {
        var result = await Run(new FakeTemplateMatcher(MatchAtExpectedPosition()), Sequence(1, Step()));

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal([new PixelPoint(550, 325)], _input.Clicks);
        Assert.Equal([new PixelRect(450, 275, 200, 100)], _capture.Captures);
    }

    [Fact]
    public async Task Click_follows_the_element_when_it_moved()
    {
        var movedBy20Right = new MatchResult(new PixelRect(70, 25, 100, 50), 0.9);

        await Run(new FakeTemplateMatcher(movedBy20Right), Sequence(1, Step()));

        Assert.Equal([new PixelPoint(570, 325)], _input.Clicks);
    }

    [Fact]
    public async Task Coordinates_and_template_size_scale_with_the_target_region()
    {
        var matcher = new FakeTemplateMatcher(new MatchResult(new PixelRect(25, 13, 50, 25), 0.9));
        var halfSizeRegion = new PixelRect(0, 0, 500, 250);

        await Run(matcher, Sequence(1, Step()), halfSizeRegion);

        Assert.Equal([new PixelSize(50, 25)], matcher.RequestedSizes);
        // Location (200, 100, 50, 25) inflated by 50% (12.5 px rounded to 13 vertically).
        Assert.Equal(new PixelRect(175, 87, 100, 51), _capture.Captures[0]);
        Assert.Equal([new PixelPoint(225, 112)], _input.Clicks);
    }

    [Fact]
    public async Task Retries_until_the_location_appears()
    {
        var matcher = new FakeTemplateMatcher(
            MatchAtExpectedPosition(0.3),
            MatchAtExpectedPosition(0.5),
            MatchAtExpectedPosition(0.9));

        var result = await Run(matcher, Sequence(1, Step()));

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(3, _capture.Captures.Count);
        Assert.Single(_input.Clicks);
    }

    [Fact]
    public async Task Fails_when_the_location_is_not_found_before_the_timeout()
    {
        _profile.Defaults.VerificationTimeoutMs = 1000;
        _profile.Defaults.RetryIntervalMs = 250;

        var result = await Run(new FakeTemplateMatcher(MatchAtExpectedPosition(0.4)), Sequence(1, Step()));

        Assert.Equal(RunStatus.VerificationFailed, result.Status);
        Assert.Equal(0.4, result.BestScore);
        Assert.NotNull(result.LastCapture);
        Assert.Equal(new PixelRect(50, 25, 100, 50), result.ExpectedBounds);
        Assert.Equal(5, _capture.Captures.Count); // t = 0, 250, 500, 750, 1000 ms
        Assert.Empty(_input.Clicks);
    }

    [Fact]
    public async Task Location_threshold_overrides_the_profile_threshold()
    {
        _location.MatchThreshold = 0.99;
        _profile.Defaults.VerificationTimeoutMs = 0;

        var result = await Run(new FakeTemplateMatcher(MatchAtExpectedPosition(0.95)), Sequence(1, Step()));

        Assert.Equal(RunStatus.VerificationFailed, result.Status);
    }

    [Fact]
    public async Task Unverified_step_clicks_without_capturing()
    {
        await Run(new FakeTemplateMatcher(MatchAtExpectedPosition()), Sequence(1, Step(verify: false)));

        Assert.Empty(_capture.Captures);
        Assert.Equal([new PixelPoint(550, 325)], _input.Clicks);
    }

    [Fact]
    public async Task Waits_the_step_delay_before_each_click()
    {
        var step = Step();
        step.Delay = new DelayRange(300, 300);

        await Run(new FakeTemplateMatcher(MatchAtExpectedPosition()), Sequence(1, step));

        Assert.Equal([TimeSpan.FromMilliseconds(300)], _clock.Delays);
    }

    [Fact]
    public async Task Repeats_the_sequence()
    {
        var result = await Run(new FakeTemplateMatcher(MatchAtExpectedPosition()), Sequence(3, Step(), Step()));

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(6, _input.Clicks.Count);
    }

    [Fact]
    public async Task Stops_when_the_user_moves_the_mouse_and_can_resume()
    {
        _input.UserMove = (1, new PixelPoint(10, 10));
        var sequence = Sequence(1, Step(), Step(), Step());
        var matcher = new FakeTemplateMatcher(MatchAtExpectedPosition());

        var result = await Run(matcher, sequence);

        Assert.Equal(RunStatus.UserTookOver, result.Status);
        Assert.Equal(new RunPosition(0, 1), result.Position);
        Assert.Single(_input.Clicks);

        var resumed = await Run(matcher, sequence, start: result.Position);

        Assert.Equal(RunStatus.Completed, resumed.Status);
        Assert.Equal(3, _input.Clicks.Count);
    }

    [Fact]
    public async Task Cancellation_stops_the_run()
    {
        using var cts = new CancellationTokenSource();
        _clock.OnDelay = () =>
        {
            if (_input.Clicks.Count == 2)
            {
                cts.Cancel();
            }
        };

        var result = await Run(new FakeTemplateMatcher(MatchAtExpectedPosition()), Sequence(0, Step()), cancellationToken: cts.Token);

        Assert.Equal(RunStatus.Cancelled, result.Status);
        Assert.Equal(2, _input.Clicks.Count);
    }

    [Fact]
    public async Task Unknown_location_is_reported()
    {
        var step = new SequenceStep { ScreenId = _screen.Id, LocationId = Guid.NewGuid() };

        var result = await Run(new FakeTemplateMatcher(MatchAtExpectedPosition()), Sequence(1, step));

        Assert.Equal(RunStatus.InvalidStep, result.Status);
    }

    [Fact]
    public async Task Sleep_is_prevented_during_the_run_only()
    {
        _profile.Defaults.VerificationTimeoutMs = 0;

        await Run(new FakeTemplateMatcher(MatchAtExpectedPosition(0)), Sequence(1, Step()));

        Assert.Equal(1, _power.TotalRequests);
        Assert.Equal(0, _power.ActiveRequests);
    }

    private SequenceStep Step(bool verify = true) =>
        new() { ScreenId = _screen.Id, LocationId = _location.Id, Verify = verify };

    private static Sequence Sequence(int repeatCount, params SequenceStep[] steps) =>
        new() { Name = "Test", RepeatCount = repeatCount, Steps = [.. steps] };

    private Task<RunResult> Run(
        ITemplateMatcher matcher,
        Sequence sequence,
        PixelRect? region = null,
        RunPosition start = default,
        CancellationToken cancellationToken = default)
    {
        var runner = new SequenceRunner(_capture, _input, matcher, _power, _clock, new Random(42));
        var screens = new Dictionary<Guid, ScreenAssets>
        {
            [_screen.Id] = new(_screen, new Dictionary<Guid, RawImage> { [_location.Id] = TestImages.Solid(100, 50) }),
        };
        return runner.RunAsync(_profile, screens, sequence, region ?? Region, start, cancellationToken: cancellationToken);
    }
}
