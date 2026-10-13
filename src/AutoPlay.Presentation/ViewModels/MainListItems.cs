using System.Globalization;
using AutoPlay.Application.Execution;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Presentation.ViewModels;

/// <summary>A screen in the main window, with a thumbnail of its capture.</summary>
public sealed class ScreenItem(Screen screen, RawImage? thumbnail)
{
    public Screen Screen { get; } = screen;

    public Guid Id => Screen.Id;

    public string Name => Screen.Name;

    public string LocationsText => Screen.Locations.Count == 1 ? "1 location" : $"{Screen.Locations.Count} locations";

    public RawImage? Thumbnail { get; } = thumbnail;
}

/// <summary>A sequence in the main window, with its schedule and the outcome of its last run.</summary>
public sealed class SequenceItem(Sequence sequence, RunSummary? lastRun, DateTimeOffset now)
{
    public Sequence Sequence { get; } = sequence;

    public Guid Id => Sequence.Id;

    public string Name => Sequence.Name;

    public string Details
    {
        get
        {
            var steps = Sequence.Steps.Count == 1 ? "1 step" : $"{Sequence.Steps.Count} steps";
            var repeat = Sequence.RepeatCount switch
            {
                0 => "until stopped",
                1 => "once",
                var count => $"{count} times",
            };
            return $"{steps}, {repeat}";
        }
    }

    /// <summary>When the sequence starts automatically, or null if it is not scheduled.</summary>
    public string? ScheduleText => Sequence.Schedule is { Enabled: true } schedule
        ? $"⏰ {DescribeDays(schedule.Days)} at {schedule.Time.ToString("HH:mm", CultureInfo.InvariantCulture)}"
        : null;

    public RunSummary? LastRun { get; } = lastRun;

    public bool LastRunSucceeded => LastRun?.Status == RunStatus.Completed;

    public bool LastRunFailed => LastRun?.Status is RunStatus.VerificationFailed or RunStatus.InvalidStep;

    public string LastRunText => LastRun is not { } run
        ? "Never run"
        : $"{Describe(run.Status)} {DescribeWhen(run.EndedAt, now)}";

    /// <summary>Details of the last run (why it did not complete), for a tooltip.</summary>
    public string? LastRunDetails => LastRun?.Message;

    private static string Describe(RunStatus status) => status switch
    {
        RunStatus.Completed => "✔ Completed",
        RunStatus.Cancelled => "■ Stopped",
        RunStatus.UserTookOver => "⏸ Paused by the mouse",
        RunStatus.VerificationFailed => "✖ Failed: location not found",
        _ => "✖ Failed",
    };

    private static string DescribeWhen(DateTimeOffset endedAt, DateTimeOffset now)
    {
        var local = endedAt.ToLocalTime();
        var today = now.ToLocalTime().Date;
        var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (local.Date == today)
        {
            return $"today at {time}";
        }

        return local.Date == today.AddDays(-1)
            ? $"yesterday at {time}"
            : $"on {local.ToString("d", CultureInfo.CurrentCulture)} at {time}";
    }

    private static string DescribeDays(List<DayOfWeek> days)
    {
        if (days.Count is 0 or 7)
        {
            return "Every day";
        }

        // Monday first, as in the sequence editor.
        var ordered = days.Distinct().OrderBy(d => ((int)d + 6) % 7);
        return string.Join(", ", ordered.Select(d => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(d)));
    }
}
