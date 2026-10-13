using AutoPlay.Application.Execution;

namespace AutoPlay.Application.Ports;

/// <summary>Driven port: storage of the outcome of the last run of each sequence.</summary>
public interface IRunHistory
{
    IReadOnlyList<RunSummary> LoadLastRuns(Guid profileId);

    /// <summary>Stores the run, replacing the previous run of the same sequence.</summary>
    void SaveLastRun(Guid profileId, RunSummary run);
}
