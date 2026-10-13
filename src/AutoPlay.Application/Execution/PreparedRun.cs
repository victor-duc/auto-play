namespace AutoPlay.Application.Execution;

/// <summary>
/// What the <see cref="SequenceRunner"/> needs to run a sequence: the sequence with the steps of the sequences it
/// runs expanded in place, and the screens it clicks on with their template images.
/// </summary>
/// <param name="StepOffsets">For each step of the original sequence, the index of its first expanded step.</param>
public sealed record PreparedRun(
    Domain.Model.Sequence Sequence,
    IReadOnlyDictionary<Guid, ScreenAssets> Screens,
    IReadOnlyList<int> StepOffsets)
{
    /// <summary>Index in the expanded sequence of a step of the original sequence (0 if out of range).</summary>
    public int ExpandedIndexOf(int originalStepIndex) =>
        originalStepIndex >= 0 && originalStepIndex < StepOffsets.Count
            ? Math.Min(StepOffsets[originalStepIndex], Math.Max(0, Sequence.Steps.Count - 1))
            : 0;
}
