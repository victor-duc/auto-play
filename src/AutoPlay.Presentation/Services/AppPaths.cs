using System.IO;

namespace AutoPlay.Presentation.Services;

/// <summary>Folders used by the application, under %LOCALAPPDATA%\AutoPlay.</summary>
public sealed class AppPaths
{
    /// <summary>%LOCALAPPDATA%\AutoPlay.</summary>
    public static string DefaultRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoPlay");

    /// <param name="root">Folder of the settings and logs.</param>
    /// <param name="profilesFolder">Folder of the profiles chosen in the settings; null for the default one.</param>
    public AppPaths(string root, string? profilesFolder = null)
    {
        Root = root;
        Profiles = string.IsNullOrWhiteSpace(profilesFolder) ? DefaultProfiles : profilesFolder;
    }

    public string Root { get; }

    /// <summary>Default folder of the profiles, in <see cref="Root"/>.</summary>
    public string DefaultProfiles => Path.Combine(Root, "Profiles");

    /// <summary>Folder of the profiles in use.</summary>
    public string Profiles { get; }

    /// <summary>Settings of the application.</summary>
    public string Settings => Path.Combine(Root, "settings.json");

    /// <summary>Rolling log files (one per day, 14 days kept).</summary>
    public string Logs => Path.Combine(Root, "Logs");
}
