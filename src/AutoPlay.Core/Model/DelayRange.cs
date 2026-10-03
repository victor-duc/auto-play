namespace AutoPlay.Core.Model;

/// <summary>A random delay range, in milliseconds, both bounds inclusive.</summary>
public sealed record DelayRange
{
    public DelayRange(int minMs, int maxMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minMs);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMs, minMs);
        MinMs = minMs;
        MaxMs = maxMs;
    }

    public int MinMs { get; }

    public int MaxMs { get; }

    public TimeSpan Next(Random random) => TimeSpan.FromMilliseconds(random.Next(MinMs, MaxMs + 1));
}
