namespace AutoPlay.Core.Model;

/// <summary>Default execution settings of a profile, used when a step or location does not override them.</summary>
public sealed class ProfileDefaults
{
    public int VerificationTimeoutMs { get; set; } = 10_000;

    public int RetryIntervalMs { get; set; } = 250;

    /// <summary>Minimum template matching score (0.0–1.0) to accept a match.</summary>
    public double MatchThreshold { get; set; } = 0.8;

    /// <summary>Fraction of the location size added on each side to build the search area.</summary>
    public double SearchMargin { get; set; } = 0.5;

    /// <summary>Distance, in pixels, beyond which a cursor move is considered a user takeover.</summary>
    public int UserTakeoverTolerancePx { get; set; } = 10;
}
