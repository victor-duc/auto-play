using AutoPlay.Application.UseCases;

namespace AutoPlay.Presentation.Services;

/// <summary>
/// Starts the scheduled sequences. <see cref="CheckAsync"/> is called periodically by the application: due runs are
/// queued and started one at a time when the application is idle, on the profile's last target region.
/// </summary>
public sealed class RunScheduler(
    ScheduleService scheduleService,
    ISequenceExecutionFlow executionFlow,
    ActivityTracker activity,
    Func<DateTime> localNow)
{
    /// <summary>A due run that cannot start within this delay (application busy) is dropped.</summary>
    public static readonly TimeSpan MaximumPostponement = TimeSpan.FromMinutes(10);

    private readonly Queue<DueRun> _pending = new();
    private DateTime _lastCheck = localNow();
    private bool _checking;

    /// <summary>Raised with a message for the user (run started, postponed, dropped or impossible).</summary>
    public event EventHandler<string>? Notification;

    public int PendingCount => _pending.Count;

    public async Task CheckAsync()
    {
        if (_checking)
        {
            return;
        }

        _checking = true;
        try
        {
            var now = localNow();
            foreach (var run in scheduleService.FindDueRuns(_lastCheck, now))
            {
                _pending.Enqueue(run);
            }

            _lastCheck = now;

            while (_pending.TryPeek(out var next))
            {
                if (now - next.ScheduledAt > MaximumPostponement)
                {
                    _pending.Dequeue();
                    Notify($"Scheduled run of '{next.Sequence.Name}' ({next.ScheduledAt:t}) skipped: AutoPlay was busy.");
                    continue;
                }

                if (activity.IsBusy || executionFlow.IsRunning)
                {
                    return;
                }

                _pending.Dequeue();
                if (next.Profile.LastTargetRegion is null)
                {
                    Notify($"Scheduled run of '{next.Sequence.Name}' skipped: run it once by hand first, to frame the target area.");
                    continue;
                }

                Notify($"Scheduled run of '{next.Sequence.Name}' started.");
                if (!await executionFlow.RunAsync(next.Profile, next.Sequence, new RunOptions(Unattended: true)))
                {
                    Notify($"Scheduled run of '{next.Sequence.Name}' could not start: check the sequence.");
                }

                now = localNow();
            }
        }
        finally
        {
            _checking = false;
        }
    }

    private void Notify(string message) => Notification?.Invoke(this, message);
}
