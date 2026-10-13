using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.Application.Execution;
using AutoPlay.Domain.Geometry;
using AutoPlay.Adapters.Windows.Hotkeys;
using AutoPlay.Adapters.Windows.Windowing;

namespace AutoPlay.App.Views;

/// <summary>
/// Small always-on-top window showing the progress of a run. It is placed outside the target region,
/// excluded from screen captures, and registers the Esc and F12 emergency stop hotkeys.
/// </summary>
[SuppressMessage("Design", "CA1001", Justification = "The hotkey is disposed when the window is closed.")]
public partial class RunStatusWindow : Window
{
    private const uint VirtualKeyEscape = 0x1B;
    private const uint VirtualKeyF12 = 0x7B;

    private readonly RunStatusViewModel _viewModel;
    private readonly PixelRect _targetRegion;
    private readonly List<GlobalHotkey> _stopHotkeys = [];
    private bool _started;

    public RunStatusWindow(RunStatusViewModel viewModel, PixelRect targetRegion)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _targetRegion = targetRegion;
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        SourceInitialized += OnSourceInitialized;
        ContentRendered += OnContentRendered;
        Closed += (_, _) => _stopHotkeys.ForEach(h => h.Dispose());
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;

        var size = WindowGeometry.GetBounds(handle).Size;
        var workArea = WindowGeometry.GetWorkArea(_targetRegion);
        WindowGeometry.SetBounds(handle, StatusWindowPlacement.Place(size, _targetRegion, workArea));
        WindowGeometry.TryExcludeFromCapture(handle);

        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        // While a run is active, Esc and F12 stop it from any window. They are released when this window closes.
        var unavailable = new List<string>();
        foreach (var (key, name) in new[] { (VirtualKeyEscape, "Esc"), (VirtualKeyF12, "F12") })
        {
            try
            {
                var hotkey = new GlobalHotkey(handle, key);
                hotkey.Pressed += (_, _) => _viewModel.Stop();
                _stopHotkeys.Add(hotkey);
            }
            catch (Win32Exception)
            {
                unavailable.Add(name);
            }
        }

        if (unavailable.Count > 0)
        {
            _viewModel.Message = $"{string.Join(" and ", unavailable)} already used by another application: "
                + "use the other key, the Stop button, or move the mouse to pause.";
        }
    }

    private async void OnContentRendered(object? sender, EventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await _viewModel.StartAsync();
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (_stopHotkeys.Any(h => h.ProcessMessage(message, wParam)))
        {
            handled = true;
        }

        return 0;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Closing the window while running stops the run.
        if (_viewModel.IsRunning)
        {
            _viewModel.Stop();
        }
    }
}
