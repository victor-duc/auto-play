using AutoPlay.Application.Execution;
using AutoPlay.Application.Ports;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Recording;
using AutoPlay.Domain.Sequencing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPlay.Application.UseCases;

/// <summary>Use cases on screens: capture, save, delete and read them.</summary>
public sealed class ScreenService(
    IScreenRepository screens,
    ISequenceRepository sequences,
    IProfileRepository profiles,
    IScreenCapture screenCapture,
    ITemplateMatcher templateMatcher,
    ILogger<ScreenService>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    public IReadOnlyList<Screen> GetScreens(Guid profileId) => screens.LoadScreens(profileId);

    public RawImage GetCapture(Guid profileId, Guid screenId) => screens.LoadScreenCapture(profileId, screenId);

    /// <summary>A reduced copy of the screen capture for lists, or null if it cannot be read.</summary>
    public RawImage? GetThumbnail(Guid profileId, Guid screenId, int maxWidth, int maxHeight)
    {
        try
        {
            return screens.LoadScreenCapture(profileId, screenId).ScaleDown(maxWidth, maxHeight);
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return null;
        }
    }

    public RawImage GetTemplate(Guid profileId, Guid screenId, Guid locationId) =>
        screens.LoadLocationTemplate(profileId, screenId, locationId);

    /// <summary>Captures the target region for a new screen and remembers it as the profile's last target region.</summary>
    public RawImage Capture(Profile profile, PixelRect targetRegion)
    {
        var capture = screenCapture.Capture(targetRegion);
        profile.LastTargetRegion = targetRegion;
        profiles.SaveProfile(profile);
        return capture;
    }

    /// <summary>
    /// Captures the target region again for an existing screen (e.g. after a game update) and re-positions each location
    /// where its image is found: first around its previous position, then anywhere in the capture. Nothing is saved:
    /// the result is reviewed in the screen editor.
    /// </summary>
    public RecaptureResult Recapture(Profile profile, Screen screen, PixelRect targetRegion)
    {
        var capture = Capture(profile, targetRegion);
        var whole = new PixelRect(0, 0, capture.Width, capture.Height);
        var notFound = new List<Guid>();
        var drafts = new List<LocationDraft>();

        foreach (var draft in ScreenBuilder.ToDrafts(screen, capture.Size))
        {
            var location = screen.FindLocation(draft.Id)!;
            var threshold = location.MatchThreshold ?? profile.Defaults.MatchThreshold;
            var found = FindLocation(profile, screen, location, draft, capture, whole, threshold);
            if (found is { } bounds)
            {
                drafts.Add(draft.MovedTo(bounds));
            }
            else
            {
                drafts.Add(draft);
                notFound.Add(draft.Id);
            }
        }

        return new RecaptureResult(capture, drafts, notFound);
    }

    /// <summary>
    /// Looks for a location on the live screen, in the profile's last target region, as a run would (same search area
    /// and threshold) but once and without clicking: tells the user whether the step would work now.
    /// </summary>
    public OperationResult<LocationTestResult> TestLocation(Profile profile, Guid screenId, Guid locationId)
    {
        if (profile.LastTargetRegion is not { } targetRegion)
        {
            return OperationResult.Failure<LocationTestResult>(
                "The target area is not known yet: record a screen or run a sequence once before testing a step.");
        }

        Screen? screen;
        RawImage template;
        try
        {
            screen = screens.LoadScreens(profile.Id).FirstOrDefault(s => s.Id == screenId);
            if (screen?.FindLocation(locationId) is null)
            {
                return OperationResult.Failure<LocationTestResult>("The screen or the location of this step does not exist any more.");
            }

            template = screens.LoadLocationTemplate(profile.Id, screenId, locationId);
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return OperationResult.Failure<LocationTestResult>(ex.Message);
        }

        var location = screen.FindLocation(locationId)!;
        var expectedBounds = CoordinateMapper.ToPixel(location.Bounds, targetRegion);
        var searchArea = SearchArea.Around(expectedBounds, targetRegion, profile.Defaults.SearchMargin);
        var match = templateMatcher.FindBestMatch(screenCapture.Capture(searchArea), template, expectedBounds.Size, location.MatchColors);
        return OperationResult.Success(new LocationTestResult(
            screen.Name,
            location.Name,
            match.Score,
            location.MatchThreshold ?? profile.Defaults.MatchThreshold,
            match.Bounds.Left + searchArea.Left - expectedBounds.Left,
            match.Bounds.Top + searchArea.Top - expectedBounds.Top));
    }

    private PixelRect? FindLocation(
        Profile profile, Screen screen, Location location, LocationDraft draft, RawImage capture, PixelRect whole, double threshold)
    {
        RawImage template;
        try
        {
            template = screens.LoadLocationTemplate(profile.Id, screen.Id, location.Id);
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return null;
        }

        var nearby = SearchArea.Around(draft.Bounds, whole, profile.Defaults.SearchMargin);
        foreach (var area in new[] { nearby, whole })
        {
            var match = templateMatcher.FindBestMatch(capture.Crop(area), template, draft.Bounds.Size, location.MatchColors);
            if (match.Score >= threshold)
            {
                return match.Bounds.Offset(area.Left, area.Top);
            }
        }

        return null;
    }

    /// <summary>Validates and saves a new or edited screen, with the template image of each location.</summary>
    public OperationResult SaveScreen(Guid profileId, ScreenDraft draft)
    {
        try
        {
            var otherNames = screens.LoadScreens(profileId).Where(s => s.Id != draft.Id).Select(s => s.Name);
            var errors = ScreenBuilder.Validate(draft.Name, draft.Locations, draft.Capture.Size, otherNames);
            if (errors.Count > 0)
            {
                return OperationResult.Failure(errors);
            }

            var (screen, templates) = ScreenBuilder.Build(draft.Id, draft.Name, draft.DefaultDelay, draft.Capture, draft.Locations);
            screens.SaveScreen(profileId, screen, draft.Capture, templates);
            return OperationResult.Success();
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return OperationResult.Failure(ex.Message);
        }
    }

    /// <summary>Returns the sequences that have steps on the screen, which deleting it would break.</summary>
    public IReadOnlyList<Sequence> GetUsages(Guid profileId, Guid screenId) =>
        SequenceValidator.FindUsages(sequences.LoadSequences(profileId), screenId);

    public void DeleteScreen(Guid profileId, Guid screenId) => screens.DeleteScreen(profileId, screenId);
}
