using System.Windows;
using AutoPlay.App.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;

namespace AutoPlay.App.Services;

/// <summary>Orchestrates the recording of a new screen (framing, capture, annotation) and the editing of an existing one.</summary>
public sealed class ScreenRecordingFlow(ScreenService screenService, IUserDialogs dialogs)
{
    /// <summary>Time given to the desktop compositor to remove the framing window before capturing.</summary>
    private static readonly TimeSpan CaptureDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Records a new screen. Returns true if it was saved.</summary>
    public async Task<bool> RecordNewAsync(Profile profile)
    {
        // The main window is hidden so that it does not cover the target area or appear in the capture.
        var mainWindow = System.Windows.Application.Current.MainWindow;
        mainWindow.Hide();
        try
        {
            var framing = new FramingWindow(
                "Capture",
                "Move and resize this window over the area to record (for a browser game: the game area only), then click Capture.",
                profile.LastTargetRegion);
            if (framing.ShowDialog() != true || framing.TargetRegion is not { } region)
            {
                return false;
            }

            await Task.Delay(CaptureDelay);
            var capture = screenService.Capture(profile, region);

            var editor = new ScreenEditorWindow(new ScreenEditorViewModel(screenService, dialogs, profile.Id, capture, null));
            return editor.ShowDialog() == true;
        }
        finally
        {
            mainWindow.Show();
            mainWindow.Activate();
        }
    }

    /// <summary>Edits the locations of an existing screen on its stored capture. Returns true if it was saved.</summary>
    public bool Edit(Profile profile, Screen screen)
    {
        var capture = screenService.GetCapture(profile.Id, screen.Id);
        var editor = new ScreenEditorWindow(new ScreenEditorViewModel(screenService, dialogs, profile.Id, capture, screen))
        {
            Owner = System.Windows.Application.Current.MainWindow,
        };
        return editor.ShowDialog() == true;
    }
}
