namespace AutoPlay.Presentation.Services;

/// <summary>Opens files and folders with the default Windows application.</summary>
public interface IShellLauncher
{
    void Open(string path);
}
