using AutoPlay.Application.Ports;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Sequencing;

namespace AutoPlay.Application.UseCases;

/// <summary>Use cases on sequences: save, duplicate, delete and read them.</summary>
public sealed class SequenceService(ISequenceRepository sequences, IScreenRepository screens)
{
    public IReadOnlyList<Sequence> GetSequences(Guid profileId) => sequences.LoadSequences(profileId);

    /// <summary>Validates and saves a new or edited sequence; its name is trimmed.</summary>
    public OperationResult Save(Guid profileId, Sequence sequence)
    {
        try
        {
            var otherNames = sequences.LoadSequences(profileId).Where(s => s.Id != sequence.Id).Select(s => s.Name);
            var errors = SequenceValidator.Validate(
                sequence.Name, sequence.RepeatCount, sequence.Steps, screens.LoadScreens(profileId), otherNames);
            if (errors.Count > 0)
            {
                return OperationResult.Failure(errors);
            }

            sequence.Name = sequence.Name.Trim();
            sequences.SaveSequence(profileId, sequence);
            return OperationResult.Success();
        }
        catch (PersistenceException ex)
        {
            return OperationResult.Failure(ex.Message);
        }
    }

    /// <summary>Saves a copy of the sequence named "&lt;name&gt; (copy)", "&lt;name&gt; (copy 2)"…</summary>
    public OperationResult<Sequence> Duplicate(Guid profileId, Guid sequenceId)
    {
        try
        {
            var all = sequences.LoadSequences(profileId);
            if (all.FirstOrDefault(s => s.Id == sequenceId) is not { } original)
            {
                return OperationResult.Failure<Sequence>("The sequence no longer exists.");
            }

            var names = all.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var name = $"{original.Name} (copy)";
            for (var i = 2; names.Contains(name); i++)
            {
                name = $"{original.Name} (copy {i})";
            }

            var copy = new Sequence
            {
                Name = name,
                RepeatCount = original.RepeatCount,
                Steps = original.Steps
                    .Select(s => new SequenceStep
                    {
                        ScreenId = s.ScreenId,
                        LocationId = s.LocationId,
                        Delay = s.Delay,
                        VerificationTimeoutMs = s.VerificationTimeoutMs,
                        Verify = s.Verify,
                    })
                    .ToList(),
            };
            sequences.SaveSequence(profileId, copy);
            return OperationResult.Success(copy);
        }
        catch (PersistenceException ex)
        {
            return OperationResult.Failure<Sequence>(ex.Message);
        }
    }

    public void Delete(Guid profileId, Guid sequenceId) => sequences.DeleteSequence(profileId, sequenceId);
}
