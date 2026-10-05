using System.Text.Json;
using AutoPlay.Application.Ports;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Adapters.Persistence;

/// <summary>
/// Stores profiles as JSON and PNG files. Layout:
/// <code>
/// root/&lt;profile-id&gt;/profile.json
///                  /screens/&lt;screen-id&gt;/screen.json, capture.png, locations/&lt;location-id&gt;.png
///                  /sequences/&lt;sequence-id&gt;.json
///                  /logs/*.png
/// </code>
/// </summary>
/// <remarks>Input/output, JSON and image errors are reported as <see cref="PersistenceException"/>.</remarks>
public sealed class FileProfileStore(string rootPath, IImageCodec imageCodec)
    : IProfileRepository, IScreenRepository, ISequenceRepository, IRunLogStore
{
    private const string ProfileFileName = "profile.json";
    private const string ScreenFileName = "screen.json";
    private const string CaptureFileName = "capture.png";

    public string RootPath { get; } = rootPath;

    public IReadOnlyList<Profile> LoadProfiles() =>
        Guard(() => LoadProfilesCore(), "The profiles could not be loaded");

    public void SaveProfile(Profile profile) =>
        Guard(() => SaveProfileCore(profile), "The profile could not be saved");

    public void DeleteProfile(Guid profileId) =>
        Guard(() => DeleteProfileCore(profileId), "The profile could not be deleted");

    public IReadOnlyList<Screen> LoadScreens(Guid profileId) =>
        Guard(() => LoadScreensCore(profileId), "The screens could not be loaded");

    public void SaveScreen(Guid profileId, Screen screen, RawImage capture, IReadOnlyDictionary<Guid, RawImage> templates) =>
        Guard(() => SaveScreenCore(profileId, screen, capture, templates), "The screen could not be saved");

    public void DeleteScreen(Guid profileId, Guid screenId) =>
        Guard(() => DeleteScreenCore(profileId, screenId), "The screen could not be deleted");

    public RawImage LoadScreenCapture(Guid profileId, Guid screenId) =>
        Guard(() => LoadScreenCaptureCore(profileId, screenId), "The screen capture could not be loaded");

    public RawImage LoadLocationTemplate(Guid profileId, Guid screenId, Guid locationId) =>
        Guard(() => LoadLocationTemplateCore(profileId, screenId, locationId), "The location image could not be loaded");

    public IReadOnlyList<Sequence> LoadSequences(Guid profileId) =>
        Guard(() => LoadSequencesCore(profileId), "The sequences could not be loaded");

    public void SaveSequence(Guid profileId, Sequence sequence) =>
        Guard(() => SaveSequenceCore(profileId, sequence), "The sequence could not be saved");

    public void DeleteSequence(Guid profileId, Guid sequenceId) =>
        Guard(() => DeleteSequenceCore(profileId, sequenceId), "The sequence could not be deleted");

    public string SaveLogImage(Guid profileId, string name, RawImage image) =>
        Guard(() => SaveLogImageCore(profileId, name, image), "The log image could not be saved");

    private List<Profile> LoadProfilesCore()
    {
        if (!Directory.Exists(RootPath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(RootPath)
            .Select(dir => Path.Combine(dir, ProfileFileName))
            .Where(File.Exists)
            .Select(ReadJson<Profile>)
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void SaveProfileCore(Profile profile) =>
        WriteJson(Path.Combine(ProfileDirectory(profile.Id), ProfileFileName), profile);

    private void DeleteProfileCore(Guid profileId) => DeleteDirectory(ProfileDirectory(profileId));

    private List<Screen> LoadScreensCore(Guid profileId)
    {
        var screensDirectory = Path.Combine(ProfileDirectory(profileId), "screens");
        if (!Directory.Exists(screensDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(screensDirectory)
            .Select(dir => Path.Combine(dir, ScreenFileName))
            .Where(File.Exists)
            .Select(ReadJson<Screen>)
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void SaveScreenCore(Guid profileId, Screen screen, RawImage capture, IReadOnlyDictionary<Guid, RawImage> templates)
    {
        var missing = screen.Locations.FirstOrDefault(l => !templates.ContainsKey(l.Id));
        if (missing is not null)
        {
            throw new ArgumentException($"No template image for location '{missing.Name}'.", nameof(templates));
        }

        var screenDirectory = ScreenDirectory(profileId, screen.Id);
        var locationsDirectory = Path.Combine(screenDirectory, "locations");

        // Rewrite the location images so that deleted locations do not leave orphan files.
        DeleteDirectory(locationsDirectory);
        Directory.CreateDirectory(locationsDirectory);
        foreach (var location in screen.Locations)
        {
            File.WriteAllBytes(Path.Combine(locationsDirectory, $"{location.Id}.png"), imageCodec.EncodePng(templates[location.Id]));
        }

        File.WriteAllBytes(Path.Combine(screenDirectory, CaptureFileName), imageCodec.EncodePng(capture));
        WriteJson(Path.Combine(screenDirectory, ScreenFileName), screen);
    }

    private void DeleteScreenCore(Guid profileId, Guid screenId) => DeleteDirectory(ScreenDirectory(profileId, screenId));

    private RawImage LoadScreenCaptureCore(Guid profileId, Guid screenId) =>
        imageCodec.Decode(File.ReadAllBytes(Path.Combine(ScreenDirectory(profileId, screenId), CaptureFileName)));

    private RawImage LoadLocationTemplateCore(Guid profileId, Guid screenId, Guid locationId) =>
        imageCodec.Decode(File.ReadAllBytes(Path.Combine(ScreenDirectory(profileId, screenId), "locations", $"{locationId}.png")));

    private List<Sequence> LoadSequencesCore(Guid profileId)
    {
        var sequencesDirectory = Path.Combine(ProfileDirectory(profileId), "sequences");
        if (!Directory.Exists(sequencesDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(sequencesDirectory, "*.json")
            .Select(ReadJson<Sequence>)
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void SaveSequenceCore(Guid profileId, Sequence sequence) =>
        WriteJson(SequencePath(profileId, sequence.Id), sequence);

    private void DeleteSequenceCore(Guid profileId, Guid sequenceId)
    {
        var path = SequencePath(profileId, sequenceId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string SaveLogImageCore(Guid profileId, string name, RawImage image)
    {
        var logsDirectory = Path.Combine(ProfileDirectory(profileId), "logs");
        Directory.CreateDirectory(logsDirectory);
        var safeName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(logsDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{safeName}.png");
        File.WriteAllBytes(path, imageCodec.EncodePng(image));
        return path;
    }

    private static T Guard<T>(Func<T> action, string message)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NotSupportedException)
        {
            throw new PersistenceException($"{message}: {ex.Message}", ex);
        }
    }

    private static void Guard(Action action, string message) =>
        Guard(() =>
        {
            action();
            return true;
        }, message);

    private string ProfileDirectory(Guid profileId) => Path.Combine(RootPath, profileId.ToString());

    private string ScreenDirectory(Guid profileId, Guid screenId) =>
        Path.Combine(ProfileDirectory(profileId), "screens", screenId.ToString());

    private string SequencePath(Guid profileId, Guid sequenceId) =>
        Path.Combine(ProfileDirectory(profileId), "sequences", $"{sequenceId}.json");

    private static T ReadJson<T>(string path) => FileFormat.Deserialize<T>(File.ReadAllText(path), path);

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temporary file first so that a crash never leaves a truncated file behind.
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, FileFormat.Serialize(value));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
