using AutoPlay.Application.Ports;

namespace AutoPlay.Application.Tests.Fakes;

/// <summary>A clock whose delays complete immediately and advance the current time.</summary>
internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public List<TimeSpan> Delays { get; } = [];

    public Action? OnDelay { get; set; }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        OnDelay?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(delay);
        UtcNow += delay;
        return Task.CompletedTask;
    }
}
