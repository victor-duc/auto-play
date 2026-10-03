using System.IO;
using System.Windows;
using AutoPlay.App.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Application.Execution;
using AutoPlay.Domain.Imaging;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Sequencing;
using AutoPlay.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPlay.App.Services;

/// <summary>Runs a sequence: loads its screens, lets the user frame the target region, then shows the run status.</summary>
public sealed class SequenceExecutionFlow(IProfileStore store, IUserDialogs dialogs, IServiceProvider services)
{
    /// <summary>Time given to the desktop compositor to remove the framing window before the first capture.</summary>
    private static readonly TimeSpan StartDelay = TimeSpan.FromMilliseconds(300);

    public async Task RunAsync(Profile profile, Sequence sequence)
    {
        if (LoadAssets(profile, sequence) is not { } screens)
        {
            return;
        }

        var mainWindow = System.Windows.Application.Current.MainWindow;
        mainWindow.Hide();
        try
        {
            var framing = new FramingWindow(
                "Start",
                "Check that this window exactly covers the target area (move or resize it if needed), then click Start.",
                profile.LastTargetRegion);
            if (framing.ShowDialog() != true || framing.TargetRegion is not { } region)
            {
                return;
            }

            profile.LastTargetRegion = region;
            store.SaveProfile(profile);
            await Task.Delay(StartDelay);

            var viewModel = new RunStatusViewModel(services.GetRequiredService<SequenceRunner>(), store, profile, sequence, screens, region);
            new RunStatusWindow(viewModel, region).ShowDialog();
        }
        finally
        {
            mainWindow.Show();
            mainWindow.Activate();
        }
    }

    /// <summary>Loads the screens used by the sequence and their template images; reports problems to the user.</summary>
    private Dictionary<Guid, ScreenAssets>? LoadAssets(Profile profile, Sequence sequence)
    {
        var screens = store.LoadScreens(profile.Id);
        if (sequence.Steps.Count == 0 || sequence.Steps.Any(step => SequenceValidator.FindLocation(screens, step) is null))
        {
            dialogs.ShowError($"The sequence '{sequence.Name}' has no steps or refers to deleted screens or locations. Edit it first.");
            return null;
        }

        var assets = new Dictionary<Guid, ScreenAssets>();
        try
        {
            foreach (var screenId in sequence.Steps.Select(s => s.ScreenId).Distinct())
            {
                var screen = screens.First(s => s.Id == screenId);
                var templates = new Dictionary<Guid, RawImage>();
                foreach (var location in screen.Locations)
                {
                    templates[location.Id] = store.LoadLocationTemplate(profile.Id, screen.Id, location.Id);
                }

                assets[screenId] = new ScreenAssets(screen, templates);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            dialogs.ShowError($"The images of the sequence could not be loaded: {ex.Message}");
            return null;
        }

        return assets;
    }
}
