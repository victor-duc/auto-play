namespace AutoPlay.Domain.Model;

/// <summary>An ordered list of steps to execute.</summary>
public sealed class Sequence
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>Number of times the steps are executed; 0 means until stopped.</summary>
    public int RepeatCount { get; set; } = 1;

    public List<SequenceStep> Steps { get; set; } = [];
}
