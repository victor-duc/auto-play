using AutoPlay.Application.Execution;
using AutoPlay.Application.Ports;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Sequencing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPlay.Application.UseCases;

/// <summary>
/// Use cases around a run: prepare what the <see cref="SequenceRunner"/> needs, and keep a diagnostic
/// capture when a verification fails.
/// </summary>
public sealed class SequenceExecutionService(
    IScreenRepository screens,
    ISequenceRepository sequences,
    IRunLogStore runLogs,
    ILogger<SequenceExecutionService>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    /// <summary>
    /// Checks that the sequence can run, expands the sequences it runs, and loads the screens it clicks on
    /// with their template images.
    /// </summary>
    public OperationResult<PreparedRun> PrepareRun(Guid profileId, Sequence sequence)
    {
        try
        {
            var expansion = SequenceExpander.ExpandWithOffsets(sequence, sequences.LoadSequences(profileId));
            if (expansion.Expanded is not { } expanded)
            {
                return OperationResult.Failure<PreparedRun>([.. expansion.Errors, "Edit the sequence first."]);
            }

            var profileScreens = screens.LoadScreens(profileId);
            var clickSteps = expanded.Steps.Where(s => s.Kind == StepKind.Click).ToList();
            if (expanded.Steps.Count == 0 || clickSteps.Any(step => SequenceValidator.FindLocation(profileScreens, step) is null))
            {
                return OperationResult.Failure<PreparedRun>(
                    $"The sequence '{sequence.Name}' has no steps or refers to deleted screens or locations. Edit it first.");
            }

            var assets = new Dictionary<Guid, ScreenAssets>();
            foreach (var screenId in clickSteps.Select(s => s.ScreenId).Distinct())
            {
                var screen = profileScreens.First(s => s.Id == screenId);
                var templates = screen.Locations.ToDictionary(
                    l => l.Id,
                    l => screens.LoadLocationTemplate(profileId, screen.Id, l.Id));
                assets[screenId] = new ScreenAssets(screen, templates);
            }

            return OperationResult.Success(new PreparedRun(expanded, assets, expansion.StepOffsets));
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return OperationResult.Failure<PreparedRun>($"The images of the sequence could not be loaded: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the last capture of a failed verification, with the expected position drawn in red,
    /// and returns where it was stored; returns null if the result has no capture.
    /// </summary>
    public string? SaveFailureCapture(Guid profileId, Sequence sequence, RunResult result)
    {
        if (result.LastCapture is not { } capture)
        {
            return null;
        }

        RawImage image = result.ExpectedBounds is { } expected ? capture.WithRectangle(expected, 255, 0, 0) : capture;
        return runLogs.SaveLogImage(profileId, $"{sequence.Name}-step{result.Position.StepIndex + 1}", image);
    }
}
