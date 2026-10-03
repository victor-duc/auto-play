using AutoPlay.Application.Ports;
using AutoPlay.Application.Tests.Fakes;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Tests.UseCases;

public class SequenceServiceTests
{
    private readonly Guid _profileId = Guid.NewGuid();
    private readonly InMemoryRepository _repository = new();
    private readonly Screen _screen;
    private readonly SequenceService _service;

    public SequenceServiceTests()
    {
        _screen = new Screen { Name = "Map", Locations = [new Location { Name = "Button" }] };
        _repository.Screens[_profileId] = [_screen];
        _service = new SequenceService(_repository, _repository);
    }

    private Sequence NewSequence(string name, int repeatCount = 1) => new()
    {
        Name = name,
        RepeatCount = repeatCount,
        Steps = [new SequenceStep { ScreenId = _screen.Id, LocationId = _screen.Locations[0].Id, Delay = new DelayRange(5, 10) }],
    };

    [Fact]
    public void Save_stores_a_valid_sequence_with_a_trimmed_name()
    {
        var result = _service.Save(_profileId, NewSequence(" Raids "));

        Assert.True(result.Succeeded);
        Assert.Equal("Raids", Assert.Single(_service.GetSequences(_profileId)).Name);
    }

    [Fact]
    public void Save_validates_against_the_screens_and_the_other_sequences()
    {
        _service.Save(_profileId, NewSequence("Raids"));
        var invalid = NewSequence("raids");
        invalid.Steps.Add(new SequenceStep { ScreenId = Guid.NewGuid(), LocationId = Guid.NewGuid() });

        var result = _service.Save(_profileId, invalid);

        Assert.Contains("Another sequence is already named 'raids'.", result.Errors);
        Assert.Contains("Step 2 refers to a screen or location that no longer exists.", result.Errors);
        Assert.Single(_service.GetSequences(_profileId));
    }

    [Fact]
    public void Save_accepts_the_same_name_when_editing_the_sequence()
    {
        var sequence = NewSequence("Raids");
        _service.Save(_profileId, sequence);
        sequence.RepeatCount = 5;

        Assert.True(_service.Save(_profileId, sequence).Succeeded);
        Assert.Equal(5, Assert.Single(_service.GetSequences(_profileId)).RepeatCount);
    }

    [Fact]
    public void Duplicate_copies_the_steps_under_a_free_name()
    {
        var original = NewSequence("Raids", repeatCount: 3);
        _service.Save(_profileId, original);
        _service.Save(_profileId, NewSequence("Raids (copy)"));

        var result = _service.Duplicate(_profileId, original.Id);

        Assert.True(result.Succeeded);
        var copy = result.Value!;
        Assert.Equal("Raids (copy 2)", copy.Name);
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(3, copy.RepeatCount);
        var step = Assert.Single(copy.Steps);
        Assert.NotSame(original.Steps[0], step);
        Assert.Equal(new DelayRange(5, 10), step.Delay);
        Assert.Equal(3, _service.GetSequences(_profileId).Count);
    }

    [Fact]
    public void Duplicate_of_a_missing_sequence_fails()
    {
        Assert.Equal(["The sequence no longer exists."], _service.Duplicate(_profileId, Guid.NewGuid()).Errors);
    }

    [Fact]
    public void Save_reports_storage_errors()
    {
        _repository.Failure = new PersistenceException("Disk full");

        Assert.Equal(["Disk full"], _service.Save(_profileId, NewSequence("Raids")).Errors);
    }

    [Fact]
    public void Delete_removes_the_sequence()
    {
        var sequence = NewSequence("Raids");
        _service.Save(_profileId, sequence);

        _service.Delete(_profileId, sequence.Id);

        Assert.Empty(_service.GetSequences(_profileId));
    }
}
