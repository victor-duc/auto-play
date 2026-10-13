using System.ComponentModel;
using System.Media;
using System.Windows;
using System.Windows.Interop;
using AutoPlay.Adapters.Windows.Hotkeys;
using AutoPlay.App.Services;
using AutoPlay.Application.Settings;
using AutoPlay.Presentation.ViewModels;

namespace AutoPlay.App.Views;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The hotkey is disposed when the window is closed.")]
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsService _settings;
    private GlobalHotkey? _quickCaptureHotkey;
    private nint _handle;

    public MainWindow(MainViewModel viewModel, SettingsService settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;
        Loaded += (_, _) => _viewModel.Load();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) =>
        {
            _settings.Changed -= OnSettingsChanged;
            _quickCaptureHotkey?.Dispose();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_handle)?.AddHook(WndProc);
        RegisterQuickCapture(_settings.Current);
        _settings.Changed += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) => RegisterQuickCapture(settings);

    /// <summary>Registers (or re-registers after a settings change) the system-wide quick capture shortcut.</summary>
    private void RegisterQuickCapture(AppSettings settings)
    {
        _quickCaptureHotkey?.Dispose();
        _quickCaptureHotkey = null;
        if (!settings.QuickCaptureEnabled || !HotkeyGesture.TryParse(settings.QuickCaptureHotkey, out var key, out var modifiers))
        {
            return;
        }

        try
        {
            _quickCaptureHotkey = new GlobalHotkey(_handle, key, modifiers);
            _quickCaptureHotkey.Pressed += OnQuickCapturePressed;
        }
        catch (Win32Exception)
        {
            _viewModel.StatusMessage =
                $"The quick capture shortcut {settings.QuickCaptureHotkey} is already used by another application: choose another one in the settings.";
        }
    }

    private void OnQuickCapturePressed(object? sender, EventArgs e)
    {
        // A sound tells the player the result without leaving the game.
        if (_viewModel.QuickCapture())
        {
            SystemSounds.Asterisk.Play();
        }
        else
        {
            SystemSounds.Hand.Play();
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_quickCaptureHotkey?.ProcessMessage(msg, wParam) == true)
        {
            handled = true;
        }

        return 0;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
