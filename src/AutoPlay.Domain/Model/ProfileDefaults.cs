namespace AutoPlay.Domain.Model;

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

    /// <summary>Returns the errors of the settings, or an empty list if they are valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (MatchThreshold is < 0 or > 1 || double.IsNaN(MatchThreshold))
        {
            errors.Add("The match threshold must be between 0 and 1.");
        }

        if (VerificationTimeoutMs < 0)
        {
            errors.Add("The verification timeout must not be negative.");
        }

        if (RetryIntervalMs < 1)
        {
            errors.Add("The retry interval must be at least 1 ms.");
        }

        if (SearchMargin is < 0 or > 10 || double.IsNaN(SearchMargin))
        {
            errors.Add("The search margin must be between 0 and 10 (0 % to 1000 %).");
        }

        if (UserTakeoverTolerancePx < 0)
        {
            errors.Add("The mouse tolerance must not be negative.");
        }

        return errors;
    }
}
