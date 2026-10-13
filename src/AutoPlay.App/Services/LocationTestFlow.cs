using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.Services;

namespace AutoPlay.App.Services;

/// <summary>Tests a location on the live screen with the AutoPlay windows out of the way.</summary>
public sealed class LocationTestFlow(ScreenService screenService) : ILocationTestFlow
{
    /// <summary>Time given to the desktop compositor to remove the AutoPlay windows before capturing.</summary>
    private static readonly TimeSpan CaptureDelay = TimeSpan.FromMilliseconds(300);

    public async Task<OperationResult<LocationTestResult>> TestAsync(Profile profile, Guid screenId, Guid locationId)
    {
        using var hidden = HiddenWindows.HideAll();
        await Task.Delay(CaptureDelay);
        return screenService.TestLocation(profile, screenId, locationId);
    }
}
