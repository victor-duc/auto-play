using AutoPlay.Application.Execution;

namespace AutoPlay.Adapters.Persistence;

/// <summary>Content of a profile's runs.json file.</summary>
internal sealed class RunHistoryFile
{
    public List<RunSummary> LastRuns { get; init; } = [];
}
