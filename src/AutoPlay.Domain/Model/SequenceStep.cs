namespace AutoPlay.Domain.Model;

/// <summary>Clicks a location of a screen. Null settings inherit from the screen or profile.</summary>
public sealed class SequenceStep
{
    public Guid ScreenId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>Overrides the screen's default delay before the click.</summary>
    public DelayRange? Delay { get; set; }

    /// <summary>Overrides the profile's verification timeout.</summary>
    public int? VerificationTimeoutMs { get; set; }

    /// <summary>Whether the location's template image must be found before clicking.</summary>
    public bool Verify { get; set; } = true;
}
