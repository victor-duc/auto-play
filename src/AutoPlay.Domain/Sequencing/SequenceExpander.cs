using AutoPlay.Domain.Model;

namespace AutoPlay.Domain.Sequencing;

/// <summary>
/// Replaces the steps that run other sequences by the steps of those sequences, repeated as many times
/// as their repeat count, so that the engine only executes clicks and waits.
/// </summary>
public static class SequenceExpander
{
    /// <summary>Maximum number of steps of an expanded sequence, which guards against accidental blow-ups.</summary>
    public const int MaximumSteps = 100_000;

    /// <summary>Returns the expanded copy of <paramref name="sequence"/>, or the reasons it cannot be expanded.</summary>
    /// <param name="allSequences">The sequences of the profile (the version of <paramref name="sequence"/> in it is ignored).</param>
    public static (Sequence? Expanded, IReadOnlyList<string> Errors) Expand(Sequence sequence, IReadOnlyList<Sequence> allSequences)
    {
        var expansion = ExpandWithOffsets(sequence, allSequences);
        return (expansion.Expanded, expansion.Errors);
    }

    /// <summary>
    /// Like <see cref="Expand"/>, and also returns, for each step of <paramref name="sequence"/>, the index of its
    /// first expanded step (to start a run from a given step).
    /// </summary>
    public static SequenceExpansion ExpandWithOffsets(Sequence sequence, IReadOnlyList<Sequence> allSequences)
    {
        var errors = new List<string>();
        var steps = new List<SequenceStep>();
        var offsets = new List<int>();
        Append(sequence, allSequences, [sequence.Id], steps, errors, offsets);
        if (errors.Count > 0)
        {
            return new SequenceExpansion(null, [], errors.Distinct().ToList());
        }

        var expanded = new Sequence
        {
            Id = sequence.Id,
            Name = sequence.Name,
            RepeatCount = sequence.RepeatCount,
            Steps = steps,
            Schedule = sequence.Schedule,
        };
        return new SequenceExpansion(expanded, offsets, []);
    }

    /// <summary>Returns the errors of the steps that run other sequences (missing, cyclic, endless).</summary>
    public static IReadOnlyList<string> ValidateChaining(Sequence sequence, IReadOnlyList<Sequence> allSequences) =>
        Expand(sequence, allSequences).Errors;

    /// <summary>Returns the sequences that run the given sequence.</summary>
    public static IReadOnlyList<Sequence> FindCallers(IEnumerable<Sequence> sequences, Guid sequenceId) =>
        sequences.Where(s => s.Steps.Any(step => step.Kind == StepKind.RunSequence && step.SequenceId == sequenceId)).ToList();

    private static void Append(
        Sequence sequence,
        IReadOnlyList<Sequence> allSequences,
        List<Guid> callStack,
        List<SequenceStep> output,
        List<string> errors,
        List<int>? offsets = null)
    {
        for (var i = 0; i < sequence.Steps.Count; i++)
        {
            offsets?.Add(output.Count);
            var step = sequence.Steps[i];
            if (step.Kind != StepKind.RunSequence)
            {
                output.Add(step.Clone());
                if (output.Count > MaximumSteps)
                {
                    errors.Add($"The sequence would have more than {MaximumSteps} steps once expanded.");
                    return;
                }

                continue;
            }

            var target = allSequences.FirstOrDefault(s => s.Id == step.SequenceId && s.Id != callStack[0]);
            if (step.SequenceId == callStack[0] || (target is not null && callStack.Contains(target.Id)))
            {
                errors.Add($"Step {i + 1} of '{sequence.Name}' creates a loop: a sequence cannot run itself, directly or not.");
                continue;
            }

            if (target is null)
            {
                errors.Add($"Step {i + 1} of '{sequence.Name}' runs a sequence that no longer exists.");
                continue;
            }

            if (target.RepeatCount == 0)
            {
                errors.Add($"Step {i + 1} of '{sequence.Name}' runs '{target.Name}', which repeats until stopped: it would never end.");
                continue;
            }

            callStack.Add(target.Id);
            for (var repeat = 0; repeat < target.RepeatCount && errors.Count == 0; repeat++)
            {
                Append(target, allSequences, callStack, output, errors);
            }

            callStack.RemoveAt(callStack.Count - 1);
        }
    }
}

/// <summary>The result of <see cref="SequenceExpander.ExpandWithOffsets"/>.</summary>
/// <param name="Expanded">The expanded sequence, or null if it cannot be expanded.</param>
/// <param name="StepOffsets">For each step of the original sequence, the index of its first expanded step.</param>
/// <param name="Errors">Why the sequence cannot be expanded.</param>
public sealed record SequenceExpansion(Sequence? Expanded, IReadOnlyList<int> StepOffsets, IReadOnlyList<string> Errors);
