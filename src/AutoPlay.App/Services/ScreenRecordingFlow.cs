using System.Windows;
using AutoPlay.App.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Core.Abstractions;
using AutoPlay.Core.Model;
using AutoPlay.Core.Storage;

namespace AutoPlay.App.Services;

/// <summary>Orchestrates the recording of a new screen (framing, capture, annotation) and the editing of an existing one.</summary>
public sealed class ScreenRecordingFlow(IScreenCapture screenCapture, IProfileStore store, IUserDialogs dialogs)
{
    /// <summary>Time given to the desktop compositor to remove the framing window before capturing.</summary>
    private static readonly TimeSpan CaptureDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Records a new screen. Returns true if it was saved.</summary>
    /// <param name="otherScreenNames">Names of the existing screens of the profile.</param>
    public async Task<bool> RecordNewAsync(Profile profile, IReadOnlyList<string> otherScreenNames)
    {
        // The main window is hidden so that it does not cover the target area or appear in the capture.
        var mainWindow = Application.Current.MainWindow;
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
            var capture = screenCapture.Capture(region);

            profile.LastTargetRegion = region;
            store.SaveProfile(profile);

            var editor = new ScreenEditorWindow(new ScreenEditorViewModel(store, dialogs, profile.Id, capture, null, otherScreenNames));
            return editor.ShowDialog() == true;
        }
        finally
        {
            mainWindow.Show();
            mainWindow.Activate();
        }
    }

    /// <summary>Edits the locations of an existing screen on its stored capture. Returns true if it was saved.</summary>
    public bool Edit(Profile profile, Screen screen, IReadOnlyList<string> otherScreenNames)
    {
        var capture = store.LoadScreenCapture(profile.Id, screen.Id);
        var editor = new ScreenEditorWindow(new ScreenEditorViewModel(store, dialogs, profile.Id, capture, screen, otherScreenNames))
        {
            Owner = Application.Current.MainWindow,
        };
        return editor.ShowDialog() == true;
    }
}
