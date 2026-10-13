using AutoPlay.Domain.Model;

namespace AutoPlay.Domain.Tests.Model;

public class SequenceScheduleTests
{
    // 2026-10-12 is a Monday.
    private static readonly DateTime Monday0800 = new(2026, 10, 12, 8, 0, 0);

    [Fact]
    public void Next_occurrence_is_today_when_the_time_is_ahead()
    {
        var schedule = new SequenceSchedule { Time = new TimeOnly(9, 30) };

        Assert.Equal(new DateTime(2026, 10, 12, 9, 30, 0), schedule.NextOccurrenceAfter(Monday0800));
    }

    [Fact]
    public void Next_occurrence_is_tomorrow_when_the_time_has_passed()
    {
        var schedule = new SequenceSchedule { Time = new TimeOnly(8, 0) };

        Assert.Equal(new DateTime(2026, 10, 13, 8, 0, 0), schedule.NextOccurrenceAfter(Monday0800));
    }

    [Fact]
    public void Next_occurrence_skips_to_an_allowed_day()
    {
        var schedule = new SequenceSchedule { Time = new TimeOnly(7, 0), Days = [DayOfWeek.Saturday, DayOfWeek.Sunday] };

        Assert.Equal(new DateTime(2026, 10, 17, 7, 0, 0), schedule.NextOccurrenceAfter(Monday0800));
    }

    [Fact]
    public void Due_only_when_the_time_falls_in_the_interval()
    {
        var schedule = new SequenceSchedule { Time = new TimeOnly(8, 0, 10) };

        Assert.True(schedule.IsDueBetween(Monday0800, Monday0800.AddSeconds(15)));
        Assert.False(schedule.IsDueBetween(Monday0800.AddSeconds(10), Monday0800.AddSeconds(25)));
        Assert.False(schedule.IsDueBetween(Monday0800, Monday0800.AddSeconds(5)));
    }

    [Fact]
    public void Disabled_schedule_is_never_due()
    {
        var schedule = new SequenceSchedule { Enabled = false, Time = new TimeOnly(8, 0, 10) };

        Assert.False(schedule.IsDueBetween(Monday0800, Monday0800.AddMinutes(1)));
    }
}
