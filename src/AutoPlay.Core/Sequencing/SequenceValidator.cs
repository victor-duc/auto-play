using AutoPlay.Core.Model;

namespace AutoPlay.Core.Sequencing;

/// <summary>Validates sequences against the screens of their profile.</summary>
public static class SequenceValidator
{
    /// <summary>Returns the validation errors, or an empty list if the sequence can be saved.</summary>
    /// <param name="screens">The screens of the profile.</param>
    /// <param name="otherSequenceNames">Names of the other sequences of the profile.</param>
    public static IReadOnlyList<string> Validate(
        string name,
        int repeatCount,
        IReadOnlyList<SequenceStep> steps,
        IReadOnlyList<Screen> screens,
        IEnumerable<string> otherSequenceNames)
    {
        var errors = new List<string>();
        var trimmedName = name.Trim();

        if (trimmedName.Length == 0)
        {
            errors.Add("The sequence name is required.");
        }
        else if (otherSequenceNames.Any(other => string.Equals(other.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add($"Another sequence is already named '{trimmedName}'.");
        }

        if (repeatCount < 0)
        {
            errors.Add("The repeat count must be 0 (until stopped) or more.");
        }

        if (steps.Count == 0)
        {
            errors.Add("Add at least one step.");
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (FindLocation(screens, step) is null)
            {
                errors.Add($"Step {i + 1} refers to a screen or location that no longer exists.");
            }

            if (step.VerificationTimeoutMs is < 0)
            {
                errors.Add($"The verification timeout of step {i + 1} must not be negative.");
            }
        }

        return errors;
    }

    /// <summary>Returns the location a step refers to, or null if its screen or location was deleted.</summary>
    public static Location? FindLocation(IReadOnlyList<Screen> screens, SequenceStep step) =>
        screens.FirstOrDefault(s => s.Id == step.ScreenId)?.FindLocation(step.LocationId);

    /// <summary>Returns the sequences that have at least one step on the given screen.</summary>
    public static IReadOnlyList<Sequence> FindUsages(IEnumerable<Sequence> sequences, Guid screenId) =>
        sequences.Where(s => s.Steps.Any(step => step.ScreenId == screenId)).ToList();
}
