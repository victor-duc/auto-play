using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Application.Ports;

/// <summary>Driven port: persistence of screens, with their capture and location template images.</summary>
public interface IScreenRepository
{
    IReadOnlyList<Screen> LoadScreens(Guid profileId);

    /// <summary>Saves a screen, its full capture and the template image of each location.</summary>
    /// <param name="templates">Template images by location ID; every location of the screen must have one.</param>
    void SaveScreen(Guid profileId, Screen screen, RawImage capture, IReadOnlyDictionary<Guid, RawImage> templates);

    void DeleteScreen(Guid profileId, Guid screenId);

    RawImage LoadScreenCapture(Guid profileId, Guid screenId);

    RawImage LoadLocationTemplate(Guid profileId, Guid screenId, Guid locationId);
}
