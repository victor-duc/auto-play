using AutoPlay.Application.Ports;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Recording;
using AutoPlay.Domain.Sequencing;

namespace AutoPlay.Application.UseCases;

/// <summary>Use cases on screens: capture, save, delete and read them.</summary>
public sealed class ScreenService(
    IScreenRepository screens,
    ISequenceRepository sequences,
    IProfileRepository profiles,
    IScreenCapture screenCapture)
{
    public IReadOnlyList<Screen> GetScreens(Guid profileId) => screens.LoadScreens(profileId);

    public RawImage GetCapture(Guid profileId, Guid screenId) => screens.LoadScreenCapture(profileId, screenId);

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
            return OperationResult.Failure(ex.Message);
        }
    }

    /// <summary>Returns the sequences that have steps on the screen, which deleting it would break.</summary>
    public IReadOnlyList<Sequence> GetUsages(Guid profileId, Guid screenId) =>
        SequenceValidator.FindUsages(sequences.LoadSequences(profileId), screenId);

    public void DeleteScreen(Guid profileId, Guid screenId) => screens.DeleteScreen(profileId, screenId);
}
