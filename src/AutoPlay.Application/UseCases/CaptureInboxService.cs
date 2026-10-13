using AutoPlay.Application.Ports;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPlay.Application.UseCases;

/// <summary>
/// Use cases of the quick capture: capture the profile's target region in one keystroke while playing, then annotate
/// the pending captures as screens later.
/// </summary>
public sealed class CaptureInboxService(
    ICaptureInbox inbox,
    IScreenCapture screenCapture,
    IClock clock,
    ILogger<CaptureInboxService>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;

    /// <summary>Captures the last target region of the profile and adds it to the pending captures.</summary>
    public OperationResult<PendingCapture> QuickCapture(Profile profile)
    {
        if (profile.LastTargetRegion is not { } region)
        {
            return OperationResult.Failure<PendingCapture>(
                "The target area is not known yet: record a screen or run a sequence once, then use the quick capture.");
        }

        var image = screenCapture.Capture(region);
        var capture = new PendingCapture(Guid.NewGuid(), clock.UtcNow);
        try
        {
            inbox.SavePendingCapture(profile.Id, capture, image);
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return OperationResult.Failure<PendingCapture>(ex.Message);
        }

        UseCaseLog.QuickCaptured(_logger, profile.Name);
        return OperationResult.Success(capture);
    }

    /// <summary>The pending captures of the profile, oldest first.</summary>
    public IReadOnlyList<PendingCapture> GetPendingCaptures(Guid profileId) =>
        [.. inbox.LoadPendingCaptures(profileId).OrderBy(c => c.CapturedAt)];

    public RawImage GetImage(Guid profileId, Guid captureId) => inbox.LoadPendingCaptureImage(profileId, captureId);

    /// <summary>Removes a pending capture, once annotated as a screen or when it is not needed.</summary>
    public OperationResult Discard(Guid profileId, Guid captureId)
    {
        try
        {
            inbox.DeletePendingCapture(profileId, captureId);
            return OperationResult.Success();
        }
        catch (PersistenceException ex)
        {
            UseCaseLog.StorageError(_logger, ex);
            return OperationResult.Failure(ex.Message);
        }
    }
}
