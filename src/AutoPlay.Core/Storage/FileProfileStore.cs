using System.Text.Json;
using AutoPlay.Core.Abstractions;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;

namespace AutoPlay.Core.Storage;

/// <summary>
/// Stores profiles as JSON and PNG files. Layout:
/// <code>
/// root/&lt;profile-id&gt;/profile.json
///                  /screens/&lt;screen-id&gt;/screen.json, capture.png, locations/&lt;location-id&gt;.png
///                  /sequences/&lt;sequence-id&gt;.json
///                  /logs/*.png
/// </code>
/// </summary>
public sealed class FileProfileStore(string rootPath, IImageCodec imageCodec) : IProfileStore
{
    private const string ProfileFileName = "profile.json";
    private const string ScreenFileName = "screen.json";
    private const string CaptureFileName = "capture.png";

    public string RootPath { get; } = rootPath;

    public IReadOnlyList<Profile> LoadProfiles()
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

    public void SaveProfile(Profile profile) =>
        WriteJson(Path.Combine(ProfileDirectory(profile.Id), ProfileFileName), profile);

    public void DeleteProfile(Guid profileId) => DeleteDirectory(ProfileDirectory(profileId));

    public IReadOnlyList<Screen> LoadScreens(Guid profileId)
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

    public void SaveScreen(Guid profileId, Screen screen, RawImage capture, IReadOnlyDictionary<Guid, RawImage> templates)
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

    public void DeleteScreen(Guid profileId, Guid screenId) => DeleteDirectory(ScreenDirectory(profileId, screenId));

    public RawImage LoadScreenCapture(Guid profileId, Guid screenId) =>
        imageCodec.Decode(File.ReadAllBytes(Path.Combine(ScreenDirectory(profileId, screenId), CaptureFileName)));

    public RawImage LoadLocationTemplate(Guid profileId, Guid screenId, Guid locationId) =>
        imageCodec.Decode(File.ReadAllBytes(Path.Combine(ScreenDirectory(profileId, screenId), "locations", $"{locationId}.png")));

    public IReadOnlyList<Sequence> LoadSequences(Guid profileId)
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

    public void SaveSequence(Guid profileId, Sequence sequence) =>
        WriteJson(SequencePath(profileId, sequence.Id), sequence);

    public void DeleteSequence(Guid profileId, Guid sequenceId)
    {
        var path = SequencePath(profileId, sequenceId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string SaveLogImage(Guid profileId, string name, RawImage image)
    {
        var logsDirectory = Path.Combine(ProfileDirectory(profileId), "logs");
        Directory.CreateDirectory(logsDirectory);
        var safeName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(logsDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss}-{safeName}.png");
        File.WriteAllBytes(path, imageCodec.EncodePng(image));
        return path;
    }

    private string ProfileDirectory(Guid profileId) => Path.Combine(RootPath, profileId.ToString());

    private string ScreenDirectory(Guid profileId, Guid screenId) =>
        Path.Combine(ProfileDirectory(profileId), "screens", screenId.ToString());

    private string SequencePath(Guid profileId, Guid sequenceId) =>
        Path.Combine(ProfileDirectory(profileId), "sequences", $"{sequenceId}.json");

    private static T ReadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions.Default)
        ?? throw new InvalidDataException($"The file '{path}' is empty.");

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temporary file first so that a crash never leaves a truncated file behind.
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions.Default));
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
