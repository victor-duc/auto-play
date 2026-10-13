namespace AutoPlay.Domain.Model;

/// <summary>One action of a sequence. Null settings inherit from the screen or profile.</summary>
public sealed class SequenceStep
{
    public StepKind Kind { get; set; } = StepKind.Click;

    /// <summary>Screen of the location to click (<see cref="StepKind.Click"/> only).</summary>
    public Guid ScreenId { get; set; }

    /// <summary>Location to click (<see cref="StepKind.Click"/> only).</summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// For a click: overrides the screen's default delay before the click. For a wait: the duration (required).
    /// </summary>
    public DelayRange? Delay { get; set; }

    /// <summary>Overrides the profile's verification timeout (<see cref="StepKind.Click"/> only).</summary>
    public int? VerificationTimeoutMs { get; set; }

    /// <summary>Whether the location's template image must be found before clicking (<see cref="StepKind.Click"/> only).</summary>
    public bool Verify { get; set; } = true;

    /// <summary>The sequence to run (<see cref="StepKind.RunSequence"/> only).</summary>
    public Guid? SequenceId { get; set; }

    public static SequenceStep Wait(DelayRange duration) => new() { Kind = StepKind.Wait, Delay = duration };

    /// <summary>Returns an independent copy of the step.</summary>
    public SequenceStep Clone() => new()
    {
        Kind = Kind,
        ScreenId = ScreenId,
        LocationId = LocationId,
        Delay = Delay,
        VerificationTimeoutMs = VerificationTimeoutMs,
        Verify = Verify,
        SequenceId = SequenceId,
    };
}
