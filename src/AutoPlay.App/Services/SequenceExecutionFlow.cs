using AutoPlay.App.Views;
using AutoPlay.Application.Execution;
using AutoPlay.Application.Settings;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.Services;
using AutoPlay.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPlay.App.Services;

/// <summary>Runs a sequence: prepares it, lets the user frame the target region, then shows the run status.</summary>
public sealed class SequenceExecutionFlow(
    SequenceExecutionService executionService,
    ProfileService profileService,
    IUserDialogs dialogs,
    IShellLauncher shell,
    ActivityTracker activity,
    SettingsService settings,
    IServiceProvider services) : ISequenceExecutionFlow
{
    /// <summary>Time given to the desktop compositor to remove the framing window before the first capture.</summary>
    private static readonly TimeSpan StartDelay = TimeSpan.FromMilliseconds(300);

    public bool IsRunning { get; private set; }

    public async Task<bool> RunAsync(Profile profile, Sequence sequence, RunOptions? options = null)
    {
        options ??= new RunOptions();
        if (IsRunning)
        {
            return false;
        }

        var preparation = executionService.PrepareRun(profile.Id, sequence);
        if (!preparation.Succeeded || preparation.Value is not { } prepared)
        {
            if (!options.Unattended)
            {
                dialogs.ShowError(string.Join(Environment.NewLine, preparation.Errors));
            }

            return false;
        }

        if (options.Unattended && profile.LastTargetRegion is null)
        {
            return false;
        }

        IsRunning = true;
        using var busy = activity.Begin();
        var mainWindow = System.Windows.Application.Current.MainWindow;
        mainWindow.Hide();
        try
        {
            var region = profile.LastTargetRegion;
            if (!options.Unattended)
            {
                var framing = new FramingWindow(
                    "Start",
                    "Check that this window exactly covers the target area (move or resize it if needed), then click Start.",
                    profile.LastTargetRegion);
                if (framing.ShowDialog() != true || framing.TargetRegion is null)
                {
                    return false;
                }

                region = framing.TargetRegion;
                profileService.RememberTargetRegion(profile, region.Value);
            }

            await Task.Delay(StartDelay);

            var start = new RunPosition(0, prepared.ExpandedIndexOf(options.StartStep));
            var viewModel = new RunStatusViewModel(
                services.GetRequiredService<SequenceRunner>(), executionService, profile, prepared.Sequence, prepared.Screens, region!.Value, shell)
            {
                StartPosition = start,
            };
            new RunStatusWindow(viewModel, region.Value, settings.Current.StopWithEscape).ShowDialog();
            return true;
        }
        finally
        {
            IsRunning = false;
            mainWindow.Show();
            mainWindow.Activate();
        }
    }
}
