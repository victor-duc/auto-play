using AutoPlay.Application.Execution;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.ViewModels;

namespace AutoPlay.Presentation.Tests;

public class MainListItemsTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 10, 15, 0, 0, TimeSpan.Zero).ToLocalTime();

    [Fact]
    public void A_sequence_never_run_says_so()
    {
        var item = new SequenceItem(new Sequence { Name = "Raids", RepeatCount = 0, Steps = [new SequenceStep()] }, null, Now);

        Assert.Equal("Never run", item.LastRunText);
        Assert.Equal("1 step, until stopped", item.Details);
        Assert.Null(item.ScheduleText);
        Assert.False(item.LastRunSucceeded);
        Assert.False(item.LastRunFailed);
    }

    [Fact]
    public void The_last_run_is_described_relative_to_today()
    {
        var sequence = new Sequence { Name = "Raids", RepeatCount = 3 };
        var completed = new RunSummary { SequenceId = sequence.Id, EndedAt = Now.AddHours(-1), Status = RunStatus.Completed };
        var failed = new RunSummary { SequenceId = sequence.Id, EndedAt = Now.AddDays(-1), Status = RunStatus.VerificationFailed, Message = "Not found" };

        var today = new SequenceItem(sequence, completed, Now);
        var yesterday = new SequenceItem(sequence, failed, Now);

        Assert.Equal($"✔ Completed today at {Now.AddHours(-1):HH:mm}", today.LastRunText);
        Assert.True(today.LastRunSucceeded);
        Assert.StartsWith("✖ Failed: location not found yesterday at", yesterday.LastRunText);
        Assert.True(yesterday.LastRunFailed);
        Assert.Equal("Not found", yesterday.LastRunDetails);
        Assert.Equal("0 steps, 3 times", today.Details);
    }

    [Fact]
    public void The_schedule_lists_the_days_from_monday()
    {
        var sequence = new Sequence
        {
            Name = "Daily",
            Schedule = new SequenceSchedule { Enabled = true, Time = new TimeOnly(8, 30), Days = [DayOfWeek.Sunday, DayOfWeek.Monday] },
        };

        Assert.Equal("⏰ Mon, Sun at 08:30", new SequenceItem(sequence, null, Now).ScheduleText);

        sequence.Schedule.Days = [];
        Assert.Equal("⏰ Every day at 08:30", new SequenceItem(sequence, null, Now).ScheduleText);

        sequence.Schedule.Enabled = false;
        Assert.Null(new SequenceItem(sequence, null, Now).ScheduleText);
    }
}
