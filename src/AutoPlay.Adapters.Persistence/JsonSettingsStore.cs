using AutoPlay.Application.Ports;
using AutoPlay.Application.Settings;

namespace AutoPlay.Adapters.Persistence;

/// <summary>Stores the application settings in a JSON file (versioned like the other files).</summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    public string Path { get; } = path;

    public AppSettings Load()
    {
        try
        {
            return File.Exists(Path) ? FileFormat.Deserialize<AppSettings>(File.ReadAllText(Path), Path) : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            throw new PersistenceException($"The settings could not be read: {ex.Message}", ex);
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temporaryPath = Path + ".tmp";
            File.WriteAllText(temporaryPath, FileFormat.Serialize(settings));
            File.Move(temporaryPath, Path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PersistenceException($"The settings could not be saved: {ex.Message}", ex);
        }
    }
}
