using System.Diagnostics;
using System.IO;
using AutoPlay.Presentation.Services;

namespace AutoPlay.App.Services;

public sealed class ShellLauncher : IShellLauncher
{
    public void Open(string path)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
