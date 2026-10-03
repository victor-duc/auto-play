using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using AutoPlay.App.ViewModels;
using AutoPlay.Core.Execution;
using AutoPlay.Core.Geometry;
using AutoPlay.Windows.Hotkeys;
using AutoPlay.Windows.Windowing;

namespace AutoPlay.App.Views;

/// <summary>
/// Small always-on-top window showing the progress of a run. It is placed outside the target region,
/// excluded from screen captures, and registers the F12 emergency stop hotkey.
/// </summary>
[SuppressMessage("Design", "CA1001", Justification = "The hotkey is disposed when the window is closed.")]
public partial class RunStatusWindow : Window
{
    private const uint VirtualKeyF12 = 0x7B;

    private readonly RunStatusViewModel _viewModel;
    private readonly PixelRect _targetRegion;
    private GlobalHotkey? _stopHotkey;
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
        Closed += (_, _) => _stopHotkey?.Dispose();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;

        var size = WindowGeometry.GetBounds(handle).Size;
        var workArea = WindowGeometry.GetWorkArea(_targetRegion);
        WindowGeometry.SetBounds(handle, StatusWindowPlacement.Place(size, _targetRegion, workArea));
        WindowGeometry.TryExcludeFromCapture(handle);

        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        try
        {
            _stopHotkey = new GlobalHotkey(handle, VirtualKeyF12);
            _stopHotkey.Pressed += (_, _) => _viewModel.Stop();
        }
        catch (Win32Exception)
        {
            _viewModel.Message = "F12 is already used by another application: use the Stop button or move the mouse to pause.";
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
        if (_stopHotkey?.ProcessMessage(message, wParam) == true)
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
