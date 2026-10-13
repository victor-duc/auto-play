using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.Services;
using AutoPlay.TestDoubles;

namespace AutoPlay.Presentation.Tests;

public class RunSchedulerTests
{
    private readonly InMemoryRepository _repository = new();
    private readonly FakeFlows _flows = new();
    private readonly ActivityTracker _activity = new();
    private readonly List<string> _messages = [];
    private readonly Profile _profile = new() { Name = "Game", LastTargetRegion = new PixelRect(0, 0, 100, 100) };
    private DateTime _now = new(2026, 10, 12, 7, 59, 55);
    private readonly RunScheduler _scheduler;

    public RunSchedulerTests()
    {
        _repository.Profiles.Add(_profile);
        _repository.SaveSequence(_profile.Id, new Sequence { Name = "Daily", Schedule = new SequenceSchedule { Time = new TimeOnly(8, 0) } });
        _scheduler = new RunScheduler(new ScheduleService(_repository, _repository), _flows, _activity, () => _now);
        _scheduler.Notification += (_, message) => _messages.Add(message);
    }

    private Task AdvanceAndCheck(TimeSpan delay)
    {
        _now += delay;
        return _scheduler.CheckAsync();
    }

    [Fact]
    public async Task Due_run_starts_unattended()
    {
        await AdvanceAndCheck(TimeSpan.FromSeconds(3));
        Assert.Empty(_flows.Runs);

        await AdvanceAndCheck(TimeSpan.FromSeconds(10));

        Assert.Equal("Daily", Assert.Single(_flows.Runs).Name);
        Assert.True(Assert.Single(_flows.RunOptions).Unattended);
        Assert.Equal(["Scheduled run of 'Daily' started."], _messages);

        await AdvanceAndCheck(TimeSpan.FromSeconds(10));
        Assert.Single(_flows.Runs);
    }

    [Fact]
    public async Task Run_waits_until_the_application_is_idle()
    {
        var busy = _activity.Begin();
        await AdvanceAndCheck(TimeSpan.FromSeconds(10));
        Assert.Empty(_flows.Runs);
        Assert.Equal(1, _scheduler.PendingCount);

        busy.Dispose();
        await AdvanceAndCheck(TimeSpan.FromMinutes(2));

        Assert.Single(_flows.Runs);
        Assert.Equal(0, _scheduler.PendingCount);
    }

    [Fact]
    public async Task Run_postponed_too_long_is_skipped()
    {
        _flows.IsRunning = true;
        await AdvanceAndCheck(TimeSpan.FromSeconds(10));

        _flows.IsRunning = false;
        await AdvanceAndCheck(TimeSpan.FromMinutes(11));

        Assert.Empty(_flows.Runs);
        Assert.Contains("skipped: AutoPlay was busy", Assert.Single(_messages));
    }

    [Fact]
    public async Task Run_needs_a_framed_target_area()
    {
        _profile.LastTargetRegion = null;

        await AdvanceAndCheck(TimeSpan.FromSeconds(10));

        Assert.Empty(_flows.Runs);
        Assert.Contains("run it once by hand first", Assert.Single(_messages));
    }

    [Fact]
    public async Task Failed_start_is_reported()
    {
        _flows.RunResult = false;

        await AdvanceAndCheck(TimeSpan.FromSeconds(10));

        Assert.Equal("Scheduled run of 'Daily' could not start: check the sequence.", _messages[^1]);
    }
}
