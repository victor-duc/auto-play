namespace AutoPlay.Application.Execution;

/// <summary>How the last run of a sequence ended, shown in the main window.</summary>
public sealed class RunSummary
{
    public Guid SequenceId { get; init; }

    public DateTimeOffset EndedAt { get; init; }

    public RunStatus Status { get; init; }

    /// <summary>Explanation of a run that did not complete (step not found, stopped…).</summary>
    public string? Message { get; init; }
}
