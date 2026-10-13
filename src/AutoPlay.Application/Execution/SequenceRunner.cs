using AutoPlay.Application.Ports;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPlay.Application.Execution;

/// <summary>
/// Executes a sequence: for each step, waits a random delay, verifies that the location is visible
/// (retrying until a timeout) and clicks it, compensating for any drift found by the template matching.
/// </summary>
public sealed class SequenceRunner(
    IScreenCapture screenCapture,
    IInputDriver inputDriver,
    ITemplateMatcher templateMatcher,
    IPowerManager powerManager,
    IClock clock,
    Random random,
    ILogger<SequenceRunner>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    /// <param name="profile">The profile providing the default settings.</param>
    /// <param name="screens">The screens referenced by the sequence, by screen ID.</param>
    /// <param name="sequence">The sequence to execute.</param>
    /// <param name="targetRegion">The current target region, in physical screen pixels.</param>
    /// <param name="start">Where to start, e.g. to resume after a user takeover.</param>
    /// <param name="progress">Receives a report before each click.</param>
    public async Task<RunResult> RunAsync(
        Profile profile,
        IReadOnlyDictionary<Guid, ScreenAssets> screens,
        Sequence sequence,
        PixelRect targetRegion,
        RunPosition start = default,
        IProgress<RunProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (targetRegion.IsEmpty)
        {
            throw new ArgumentException("The target region must not be empty.", nameof(targetRegion));
        }

        using var sleepPrevention = powerManager.PreventSleep();
        RunLog.Started(_logger, sequence.Name, start.Iteration + 1, start.StepIndex + 1, targetRegion);

        var result = await RunIterationsAsync(profile, screens, sequence, targetRegion, start, progress, cancellationToken).ConfigureAwait(false);
        if (result.Status is RunStatus.Completed or RunStatus.Cancelled or RunStatus.UserTookOver)
        {
            RunLog.Finished(_logger, sequence.Name, result.Status, result.Position.Iteration + 1, result.Position.StepIndex + 1, result.Message);
        }
        else
        {
            RunLog.Failed(_logger, sequence.Name, result.Status, result.Position.Iteration + 1, result.Position.StepIndex + 1, result.Message);
        }

        return result;
    }

    private async Task<RunResult> RunIterationsAsync(
        Profile profile,
        IReadOnlyDictionary<Guid, ScreenAssets> screens,
        Sequence sequence,
        PixelRect targetRegion,
        RunPosition start,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken)
    {
        var position = start;
        PixelPoint? lastClick = null;
        try
        {
            for (var iteration = start.Iteration; sequence.RepeatCount == 0 || iteration < sequence.RepeatCount; iteration++)
            {
                var firstStep = iteration == start.Iteration ? start.StepIndex : 0;
                for (var stepIndex = firstStep; stepIndex < sequence.Steps.Count; stepIndex++)
                {
                    position = new RunPosition(iteration, stepIndex);
                    var outcome = await RunStepAsync(profile, screens, sequence, position, targetRegion, lastClick, progress, cancellationToken).ConfigureAwait(false);
                    if (outcome.Failure is not null)
                    {
                        return outcome.Failure;
                    }

                    lastClick = outcome.Click;
                }

                if (sequence.Steps.Count == 0)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new RunResult(RunStatus.Cancelled, position) { Message = "The run was stopped." };
        }

        return new RunResult(RunStatus.Completed, position);
    }

    private async Task<(PixelPoint? Click, RunResult? Failure)> RunStepAsync(
        Profile profile,
        IReadOnlyDictionary<Guid, ScreenAssets> screens,
        Sequence sequence,
        RunPosition position,
        PixelRect targetRegion,
        PixelPoint? lastClick,
        IProgress<RunProgress>? progress,
        CancellationToken cancellationToken)
    {
        var step = sequence.Steps[position.StepIndex];
        if (!screens.TryGetValue(step.ScreenId, out var assets)
            || assets.Screen.FindLocation(step.LocationId) is not { } location
            || !assets.Templates.TryGetValue(location.Id, out var template))
        {
            return (null, new RunResult(RunStatus.InvalidStep, position)
            {
                Message = $"Step {position.StepIndex + 1} references a screen or location that does not exist.",
            });
        }

        var defaults = profile.Defaults;
        await clock.DelayAsync((step.Delay ?? assets.Screen.DefaultDelay).Next(random), cancellationToken).ConfigureAwait(false);

        var expectedBounds = CoordinateMapper.ToPixel(location.Bounds, targetRegion);
        var clickPoint = CoordinateMapper.ToPixel(location.ClickPoint, targetRegion);
        double? score = null;

        if (step.Verify)
        {
            var timeout = TimeSpan.FromMilliseconds(step.VerificationTimeoutMs ?? defaults.VerificationTimeoutMs);
            var threshold = location.MatchThreshold ?? defaults.MatchThreshold;
            var verification = await VerifyAsync(template, expectedBounds, targetRegion, threshold, timeout, defaults, cancellationToken).ConfigureAwait(false);
            if (verification.Match is not { } match)
            {
                return (null, new RunResult(RunStatus.VerificationFailed, position)
                {
                    Message = $"'{assets.Screen.Name} / {location.Name}' was not found (best score {verification.BestScore:0.00}, threshold {threshold:0.00}).",
                    BestScore = verification.BestScore,
                    LastCapture = verification.LastCapture,
                    ExpectedBounds = verification.ExpectedBoundsInCapture,
                });
            }

            clickPoint = clickPoint.Offset(match.Bounds.Left - expectedBounds.Left, match.Bounds.Top - expectedBounds.Top);
            score = match.Score;
        }

        if (lastClick is { } previous
            && inputDriver.GetCursorPosition().DistanceTo(previous) > defaults.UserTakeoverTolerancePx)
        {
            return (null, new RunResult(RunStatus.UserTookOver, position) { Message = "The mouse was moved by the user." });
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new RunProgress(position, sequence.Steps.Count, score));
        RunLog.Click(_logger, position.Iteration + 1, position.StepIndex + 1, assets.Screen.Name, location.Name, score, clickPoint.X, clickPoint.Y);
        inputDriver.Click(clickPoint);
        return (clickPoint, null);
    }

    private async Task<Verification> VerifyAsync(
        RawImage template,
        PixelRect expectedBounds,
        PixelRect targetRegion,
        double threshold,
        TimeSpan timeout,
        ProfileDefaults defaults,
        CancellationToken cancellationToken)
    {
        var searchArea = expectedBounds
            .Inflate(
                CoordinateMapper.Round(expectedBounds.Width * defaults.SearchMargin),
                CoordinateMapper.Round(expectedBounds.Height * defaults.SearchMargin))
            .Intersect(targetRegion);
        var expectedInCapture = expectedBounds.Offset(-searchArea.Left, -searchArea.Top);
        var deadline = clock.UtcNow + timeout;
        var bestScore = 0.0;

        while (true)
        {
            var capture = screenCapture.Capture(searchArea);
            var result = templateMatcher.FindBestMatch(capture, template, expectedBounds.Size);
            bestScore = Math.Max(bestScore, result.Score);

            if (result.Score >= threshold)
            {
                return new Verification(
                    new MatchResult(result.Bounds.Offset(searchArea.Left, searchArea.Top), result.Score),
                    bestScore, capture, expectedInCapture);
            }

            if (clock.UtcNow >= deadline)
            {
                return new Verification(null, bestScore, capture, expectedInCapture);
            }

            await clock.DelayAsync(TimeSpan.FromMilliseconds(defaults.RetryIntervalMs), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <param name="Match">The accepted match, in screen coordinates, or null if none was found.</param>
    private sealed record Verification(MatchResult? Match, double BestScore, RawImage LastCapture, PixelRect ExpectedBoundsInCapture);
}

/// <summary>Log messages of the sequence engine (source-generated for performance).</summary>
internal static partial class RunLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Run of '{Sequence}' started at iteration {Iteration}, step {Step}, in region {Region}")]
    public static partial void Started(ILogger logger, string sequence, int iteration, int step, PixelRect region);

    [LoggerMessage(Level = LogLevel.Information, Message = "Iteration {Iteration}, step {Step}: '{Screen} / {Location}' (score {Score}) clicked at ({X}, {Y})")]
    public static partial void Click(ILogger logger, int iteration, int step, string screen, string location, double? score, int x, int y);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run of '{Sequence}' ended: {Status} at iteration {Iteration}, step {Step}. {Message}")]
    public static partial void Finished(ILogger logger, string sequence, RunStatus status, int iteration, int step, string? message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run of '{Sequence}' failed: {Status} at iteration {Iteration}, step {Step}. {Message}")]
    public static partial void Failed(ILogger logger, string sequence, RunStatus status, int iteration, int step, string? message);
}
