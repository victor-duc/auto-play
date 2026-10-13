using AutoPlay.Application.Execution;
using AutoPlay.Application.Ports;
using AutoPlay.TestDoubles;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Tests.UseCases;

public class SequenceExecutionServiceTests
{
    private readonly Guid _profileId = Guid.NewGuid();
    private readonly InMemoryRepository _repository = new();
    private readonly Screen _screen;
    private readonly SequenceExecutionService _service;

    public SequenceExecutionServiceTests()
    {
        _screen = new Screen { Name = "Map", Locations = [new Location { Name = "A" }, new Location { Name = "B" }] };
        _repository.Screens[_profileId] = [_screen];
        foreach (var location in _screen.Locations)
        {
            _repository.Templates[(_screen.Id, location.Id)] = TestImages.Solid(4, 4);
        }

        _service = new SequenceExecutionService(_repository, _repository, _repository);
    }

    private Sequence SequenceOn(params Guid[] locationIds) => new()
    {
        Name = "Raids",
        Steps = locationIds.Select(id => new SequenceStep { ScreenId = _screen.Id, LocationId = id }).ToList(),
    };

    [Fact]
    public void PrepareRun_loads_the_screens_and_their_templates()
    {
        var result = _service.PrepareRun(_profileId, SequenceOn(_screen.Locations[0].Id));

        Assert.True(result.Succeeded);
        var assets = Assert.Single(result.Value!.Screens).Value;
        Assert.Same(_screen, assets.Screen);
        Assert.Equal(2, assets.Templates.Count);
    }

    [Fact]
    public void PrepareRun_accepts_wait_steps()
    {
        var sequence = SequenceOn(_screen.Locations[0].Id);
        sequence.Steps.Insert(0, SequenceStep.Wait(new DelayRange(1, 1)));

        var result = _service.PrepareRun(_profileId, sequence);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.Screens);
    }

    [Fact]
    public void PrepareRun_expands_the_sequences_run_by_the_sequence()
    {
        var inner = SequenceOn(_screen.Locations[1].Id);
        inner.RepeatCount = 2;
        _repository.SaveSequence(_profileId, inner);
        var root = SequenceOn(_screen.Locations[0].Id);
        root.Steps.Add(new SequenceStep { Kind = StepKind.RunSequence, SequenceId = inner.Id });
        root.Steps.Add(SequenceStep.Wait(new DelayRange(1, 1)));

        var result = _service.PrepareRun(_profileId, root);

        Assert.True(result.Succeeded);
        Assert.Equal(
            [_screen.Locations[0].Id, _screen.Locations[1].Id, _screen.Locations[1].Id, Guid.Empty],
            result.Value!.Sequence.Steps.Select(s => s.LocationId));
        Assert.Equal(0, result.Value.ExpandedIndexOf(0));
        Assert.Equal(1, result.Value.ExpandedIndexOf(1));
        Assert.Equal(3, result.Value.ExpandedIndexOf(2));
        Assert.Equal(0, result.Value.ExpandedIndexOf(99));
    }

    [Fact]
    public void PrepareRun_refuses_broken_chaining()
    {
        var root = SequenceOn(_screen.Locations[0].Id);
        root.Steps.Add(new SequenceStep { Kind = StepKind.RunSequence, SequenceId = Guid.NewGuid() });

        var result = _service.PrepareRun(_profileId, root);

        Assert.Equal(["Step 2 of 'Raids' runs a sequence that no longer exists.", "Edit the sequence first."], result.Errors);
    }

    [Fact]
    public void PrepareRun_refuses_an_empty_sequence()
    {
        Assert.False(_service.PrepareRun(_profileId, SequenceOn()).Succeeded);
    }

    [Fact]
    public void PrepareRun_refuses_steps_on_deleted_locations()
    {
        var result = _service.PrepareRun(_profileId, SequenceOn(Guid.NewGuid()));

        Assert.Equal(
            ["The sequence 'Raids' has no steps or refers to deleted screens or locations. Edit it first."],
            result.Errors);
    }

    [Fact]
    public void PrepareRun_reports_missing_images()
    {
        _repository.Templates.Clear();

        var result = _service.PrepareRun(_profileId, SequenceOn(_screen.Locations[0].Id));

        Assert.Equal(["The images of the sequence could not be loaded: Missing template."], result.Errors);
    }

    [Fact]
    public void SaveFailureCapture_saves_the_capture_with_the_expected_position_in_red()
    {
        var result = new RunResult(RunStatus.VerificationFailed, new RunPosition(0, 2))
        {
            LastCapture = TestImages.Solid(10, 10, 0),
            ExpectedBounds = new PixelRect(2, 2, 4, 4),
        };

        var path = _service.SaveFailureCapture(_profileId, SequenceOn(), result);

        Assert.Equal("logs/Raids-step3.png", path);
        var (name, image) = Assert.Single(_repository.LogImages);
        Assert.Equal("Raids-step3", name);
        Assert.Equal(255, image.Pixels[(2 * image.Width + 2) * 4 + 2]); // Red outline.
        Assert.Equal(0, result.LastCapture.Pixels[(2 * image.Width + 2) * 4 + 2]); // Original untouched.
    }

    [Fact]
    public void SaveFailureCapture_without_capture_saves_nothing()
    {
        var path = _service.SaveFailureCapture(_profileId, SequenceOn(), new RunResult(RunStatus.Cancelled, default));

        Assert.Null(path);
        Assert.Empty(_repository.LogImages);
    }
}
