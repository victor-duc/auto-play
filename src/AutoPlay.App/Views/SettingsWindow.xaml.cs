using System.Windows;
using System.Windows.Input;
using AutoPlay.App.Services;
using AutoPlay.Presentation.ViewModels;

namespace AutoPlay.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += (_, saved) => DialogResult = saved;
    }

    /// <summary>Records the pressed key combination as the quick capture shortcut.</summary>
    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape or Key.Enter)
        {
            return; // Keep the usual navigation keys.
        }

        e.Handled = true;
        if (HotkeyGesture.Format(key, Keyboard.Modifiers) is { } gesture)
        {
            _viewModel.QuickCaptureHotkey = gesture;
        }
    }
}
