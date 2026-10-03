using AutoPlay.App.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Application.Execution;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPlay.App.Services;

/// <summary>Runs a sequence: loads its screens, lets the user frame the target region, then shows the run status.</summary>
public sealed class SequenceExecutionFlow(
    SequenceExecutionService executionService,
    ProfileService profileService,
    IUserDialogs dialogs,
    IServiceProvider services)
{
    /// <summary>Time given to the desktop compositor to remove the framing window before the first capture.</summary>
    private static readonly TimeSpan StartDelay = TimeSpan.FromMilliseconds(300);

    public async Task RunAsync(Profile profile, Sequence sequence)
    {
        var preparation = executionService.PrepareRun(profile.Id, sequence);
        if (!preparation.Succeeded || preparation.Value is not { } screens)
        {
            dialogs.ShowError(string.Join(Environment.NewLine, preparation.Errors));
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

            profileService.RememberTargetRegion(profile, region);
            await Task.Delay(StartDelay);

            var viewModel = new RunStatusViewModel(
                services.GetRequiredService<SequenceRunner>(), executionService, profile, sequence, screens, region);
            new RunStatusWindow(viewModel, region).ShowDialog();
        }
        finally
        {
            mainWindow.Show();
            mainWindow.Activate();
        }
    }
}
