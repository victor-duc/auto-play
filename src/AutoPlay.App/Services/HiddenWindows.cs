using System.Windows;

namespace AutoPlay.App.Services;

/// <summary>
/// Gets the AutoPlay windows out of the way of the target application (capture, run) until disposed.
/// The main window is hidden; the other windows are minimized, because hiding a modal dialog would close it.
/// </summary>
public sealed class HiddenWindows : IDisposable
{
    private readonly Window? _mainWindow;
    private readonly List<(Window Window, WindowState State)> _minimized = [];

    private HiddenWindows()
    {
        var application = System.Windows.Application.Current;
        foreach (var window in application.Windows.OfType<Window>().Where(w => w.IsVisible))
        {
            if (window == application.MainWindow)
            {
                _mainWindow = window;
                window.Hide();
            }
            else if (window.WindowState != WindowState.Minimized)
            {
                _minimized.Add((window, window.WindowState));
                window.WindowState = WindowState.Minimized;
            }
        }
    }

    public static HiddenWindows HideAll() => new();

    public void Dispose()
    {
        _mainWindow?.Show();
        foreach (var (window, state) in _minimized)
        {
            window.WindowState = state;
        }

        // The last window that was open (e.g. the editor that started the run) gets the focus back.
        var active = _minimized.Count > 0 ? _minimized[^1].Window : _mainWindow;
        active?.Activate();
    }
}
