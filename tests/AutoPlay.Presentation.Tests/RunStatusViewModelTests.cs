using AutoPlay.Application.Execution;
using AutoPlay.Application.Ports;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.TestDoubles;

namespace AutoPlay.Presentation.Tests;

public class RunStatusViewModelTests
{
    private static readonly PixelRect Region = new(0, 0, 1000, 500);

    private readonly InMemoryRepository _repository = new();
    private readonly FakeShell _shell = new();
    private readonly FakeInputDriver _input = new();
    private readonly Profile _profile = new() { Name = "Game", Defaults = { VerificationTimeoutMs = 0 } };
    private readonly Location _location = new()
    {
        Name = "Button",
        Bounds = new NormalizedRect(0.4, 0.4, 0.1, 0.1),
        ClickPoint = new NormalizedPoint(0.45, 0.45),
    };

    private readonly Screen _screen;
    private readonly Dictionary<Guid, ScreenAssets> _assets;

    public RunStatusViewModelTests()
    {
        _screen = new Screen { Name = "Map", DefaultDelay = new DelayRange(0, 0), Locations = [_location] };
        _assets = new() { [_screen.Id] = new(_screen, new Dictionary<Guid, RawImage> { [_location.Id] = TestImages.Solid(100, 50) }) };
    }

    private Sequence Sequence(int steps) => new()
    {
        Name = "Raids",
        Steps = Enumerable.Range(0, steps).Select(_ => new SequenceStep { ScreenId = _screen.Id, LocationId = _location.Id }).ToList(),
    };

    private RunStatusViewModel NewViewModel(Sequence sequence, double score) => new(
        new SequenceRunner(new FakeScreenCapture(), _input, new FakeTemplateMatcher(new MatchResult(new PixelRect(50, 25, 100, 50), score)), new FakePowerManager(), new FakeClock(), new Random(1)),
        new SequenceExecutionService(_repository, _repository, _repository),
        _profile,
        sequence,
        _assets,
        Region,
        _shell);

    [Fact]
    public async Task Completed_run()
    {
        var viewModel = NewViewModel(Sequence(2), score: 0.95);

        await viewModel.StartAsync();

        Assert.Equal(RunState.Completed, viewModel.State);
        Assert.Equal("Completed", viewModel.StateText);
        Assert.Equal(2, _input.Clicks.Count);
        Assert.False(viewModel.ResumeCommand.CanExecute(null));
        Assert.True(viewModel.CloseCommand.CanExecute(null));
    }

    [Fact]
    public async Task Verification_failure_saves_the_capture_and_can_be_retried()
    {
        var viewModel = NewViewModel(Sequence(1), score: 0.1);

        await viewModel.StartAsync();

        Assert.Equal(RunState.Failed, viewModel.State);
        Assert.Equal("logs/Raids-step1.png", viewModel.LogImagePath);
        Assert.True(viewModel.ResumeCommand.CanExecute(null));

        viewModel.OpenLogImageCommand.Execute(null);
        Assert.Equal(["logs/Raids-step1.png"], _shell.Opened);
    }

    [Fact]
    public async Task User_takeover_pauses_and_resume_continues()
    {
        _input.UserMove = (1, new PixelPoint(0, 0));
        var viewModel = NewViewModel(Sequence(3), score: 0.95);

        await viewModel.StartAsync();

        Assert.Equal(RunState.Paused, viewModel.State);
        Assert.Single(_input.Clicks);

        await viewModel.ResumeCommand.ExecuteAsync(null);

        Assert.Equal(RunState.Completed, viewModel.State);
        Assert.Equal(3, _input.Clicks.Count);
    }
}
