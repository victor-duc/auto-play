using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.TestDoubles;

namespace AutoPlay.Application.Tests.UseCases;

public class ScheduleServiceTests
{
    private static readonly DateTime Monday0800 = new(2026, 10, 12, 8, 0, 0);

    private readonly InMemoryRepository _repository = new();
    private readonly ScheduleService _service;

    public ScheduleServiceTests() => _service = new ScheduleService(_repository, _repository);

    private Sequence Add(Profile profile, string name, SequenceSchedule? schedule)
    {
        var sequence = new Sequence { Name = name, Schedule = schedule };
        _repository.SaveSequence(profile.Id, sequence);
        return sequence;
    }

    [Fact]
    public void Finds_the_due_runs_of_all_profiles_in_time_order()
    {
        var game = new Profile { Name = "Game" };
        var other = new Profile { Name = "Other" };
        _repository.Profiles.AddRange([game, other]);
        var late = Add(game, "Late", new SequenceSchedule { Time = new TimeOnly(8, 0, 20) });
        var early = Add(other, "Early", new SequenceSchedule { Time = new TimeOnly(8, 0, 10) });
        Add(game, "Unscheduled", null);
        Add(game, "Disabled", new SequenceSchedule { Enabled = false, Time = new TimeOnly(8, 0, 15) });
        Add(game, "Tomorrow", new SequenceSchedule { Time = new TimeOnly(7, 0) });

        var due = _service.FindDueRuns(Monday0800, Monday0800.AddSeconds(30));

        Assert.Equal([early.Id, late.Id], due.Select(d => d.Sequence.Id));
        Assert.Equal(other.Id, due[0].Profile.Id);
        Assert.Equal(new DateTime(2026, 10, 12, 8, 0, 10), due[0].ScheduledAt);
    }

    [Fact]
    public void Empty_or_reversed_intervals_have_no_runs()
    {
        var game = new Profile { Name = "Game" };
        _repository.Profiles.Add(game);
        Add(game, "S", new SequenceSchedule { Time = new TimeOnly(8, 0, 10) });

        Assert.Empty(_service.FindDueRuns(Monday0800.AddMinutes(1), Monday0800));
    }

    [Fact]
    public void Storage_errors_give_no_runs()
    {
        _repository.Failure = new AutoPlay.Application.Ports.PersistenceException("Disk unavailable");

        Assert.Empty(_service.FindDueRuns(Monday0800, Monday0800.AddMinutes(1)));
    }
}
