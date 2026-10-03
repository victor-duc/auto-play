namespace AutoPlay.Application.Ports;

/// <summary>Time source of the execution engine, replaceable in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
