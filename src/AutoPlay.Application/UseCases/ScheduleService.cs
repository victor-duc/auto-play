using AutoPlay.Application.Ports;
using AutoPlay.Domain.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPlay.Application.UseCases;

/// <summary>A scheduled run that is due.</summary>
public sealed record DueRun(Profile Profile, Sequence Sequence, DateTime ScheduledAt);

/// <summary>Finds the sequences whose automatic start is due, across all profiles.</summary>
public sealed class ScheduleService(
    IProfileRepository profiles,
    ISequenceRepository sequences,
    ILogger<ScheduleService>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    /// <summary>Returns the runs scheduled in the interval (<paramref name="from"/>, <paramref name="to"/>] (local times).</summary>
    public IReadOnlyList<DueRun> FindDueRuns(DateTime from, DateTime to)
    {
        if (to <= from)
        {
            return [];
        }

        try
        {
            return profiles.LoadProfiles()
                .SelectMany(profile => sequences.LoadSequences(profile.Id)
                    .Where(s => s.Schedule?.IsDueBetween(from, to) == true)
                    .Select(s => new DueRun(profile, s, s.Schedule!.NextOccurrenceAfter(from))))
                .OrderBy(r => r.ScheduledAt)
                .ToList();
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return [];
        }
    }
}
