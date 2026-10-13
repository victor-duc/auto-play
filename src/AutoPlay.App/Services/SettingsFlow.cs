using AutoPlay.App.Views;
using AutoPlay.Application.Settings;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.Services;
using AutoPlay.Presentation.ViewModels;

namespace AutoPlay.App.Services;

/// <summary>Opens the settings window.</summary>
public sealed class SettingsFlow(
    SettingsService settingsService,
    ProfileService profileService,
    IUserDialogs dialogs,
    AppPaths paths,
    ActivityTracker activity) : ISettingsFlow
{
    public bool Open(Profile? profile)
    {
        using var busy = activity.Begin();
        var viewModel = new SettingsViewModel(settingsService, profileService, dialogs, profile, paths.DefaultProfiles);
        var window = new SettingsWindow(viewModel) { Owner = System.Windows.Application.Current.MainWindow };
        return window.ShowDialog() == true;
    }
}
