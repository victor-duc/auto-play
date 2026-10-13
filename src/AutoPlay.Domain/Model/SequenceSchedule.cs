namespace AutoPlay.Domain.Model;

/// <summary>When a sequence starts automatically (while AutoPlay is running).</summary>
public sealed class SequenceSchedule
{
    public bool Enabled { get; set; } = true;

    /// <summary>Local time of the day at which the sequence starts.</summary>
    public TimeOnly Time { get; set; }

    /// <summary>Days on which the sequence starts; empty means every day.</summary>
    public List<DayOfWeek> Days { get; set; } = [];

    /// <summary>Returns the first start strictly after <paramref name="after"/> (local time).</summary>
    public DateTime NextOccurrenceAfter(DateTime after)
    {
        var candidate = after.Date + Time.ToTimeSpan();
        if (candidate <= after)
        {
            candidate = candidate.AddDays(1);
        }

        for (var i = 0; i < 7 && Days.Count > 0 && !Days.Contains(candidate.DayOfWeek); i++)
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    /// <summary>Whether a start falls in the interval (<paramref name="from"/>, <paramref name="to"/>].</summary>
    public bool IsDueBetween(DateTime from, DateTime to) => Enabled && NextOccurrenceAfter(from) <= to;
}
