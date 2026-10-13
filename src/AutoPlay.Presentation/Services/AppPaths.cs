using System.IO;

namespace AutoPlay.Presentation.Services;

/// <summary>Folders used by the application, under %LOCALAPPDATA%\AutoPlay.</summary>
public sealed class AppPaths
{
    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoPlay"));

    public AppPaths(string root)
    {
        Root = root;
    }

    public string Root { get; }

    /// <summary>Default folder of the profiles.</summary>
    public string Profiles => Path.Combine(Root, "Profiles");

    /// <summary>Rolling log files (one per day, 14 days kept).</summary>
    public string Logs => Path.Combine(Root, "Logs");
}
