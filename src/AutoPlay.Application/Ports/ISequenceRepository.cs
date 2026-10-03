using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Ports;

/// <summary>Driven port: persistence of sequences.</summary>
public interface ISequenceRepository
{
    IReadOnlyList<Sequence> LoadSequences(Guid profileId);

    void SaveSequence(Guid profileId, Sequence sequence);

    void DeleteSequence(Guid profileId, Guid sequenceId);
}
