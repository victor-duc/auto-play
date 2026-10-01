using AutoPlay.Core.Imaging;
using AutoPlay.Core.Model;

namespace AutoPlay.Core.Storage;

/// <summary>Persists profiles, screens (with their images) and sequences.</summary>
public interface IProfileStore
{
    IReadOnlyList<Profile> LoadProfiles();

    void SaveProfile(Profile profile);

    void DeleteProfile(Guid profileId);

    IReadOnlyList<Screen> LoadScreens(Guid profileId);

    /// <summary>Saves a screen, its full capture and the template image of each location.</summary>
    /// <param name="templates">Template images by location ID; every location of the screen must have one.</param>
    void SaveScreen(Guid profileId, Screen screen, RawImage capture, IReadOnlyDictionary<Guid, RawImage> templates);

    void DeleteScreen(Guid profileId, Guid screenId);

    RawImage LoadScreenCapture(Guid profileId, Guid screenId);

    RawImage LoadLocationTemplate(Guid profileId, Guid screenId, Guid locationId);

    IReadOnlyList<Sequence> LoadSequences(Guid profileId);

    void SaveSequence(Guid profileId, Sequence sequence);

    void DeleteSequence(Guid profileId, Guid sequenceId);

    /// <summary>Saves a diagnostic image in the profile's log folder and returns its path.</summary>
    string SaveLogImage(Guid profileId, string name, RawImage image);
}
