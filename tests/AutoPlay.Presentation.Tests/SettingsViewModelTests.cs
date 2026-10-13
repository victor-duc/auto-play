using AutoPlay.Application.Ports;
using AutoPlay.Application.Settings;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.TestDoubles;

namespace AutoPlay.Presentation.Tests;

public class SettingsViewModelTests
{
    private readonly InMemoryRepository _repository = new();
    private readonly MemorySettingsStore _store = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly SettingsService _settings;
    private readonly Profile _profile = new() { Name = "Game", Defaults = { MatchThreshold = 0.75 } };

    public SettingsViewModelTests()
    {
        _settings = new SettingsService(_store);
        _repository.Profiles.Add(_profile);
    }

    private SettingsViewModel NewViewModel(Profile? profile) =>
        new(_settings, new ProfileService(_repository), _dialogs, profile, "default-folder");

    [Fact]
    public void Shows_the_current_settings_and_profile_defaults()
    {
        _store.Saved = new AppSettings { Theme = ThemePreference.Dark, StopWithEscape = false, DataFolder = "/data" };

        var viewModel = NewViewModel(_profile);

        Assert.Equal(ThemePreference.Dark, viewModel.Theme);
        Assert.False(viewModel.StopWithEscape);
        Assert.Equal("/data", viewModel.DataFolder);
        Assert.Equal(0.75, double.Parse(viewModel.MatchThresholdText.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("50", viewModel.SearchMarginText);
        Assert.True(viewModel.HasProfile);
        Assert.Equal("Defaults of the profile 'Game'", viewModel.ProfileTitle);
    }

    [Fact]
    public void Save_stores_the_settings_and_the_defaults()
    {
        var viewModel = NewViewModel(_profile);
        bool? closed = null;
        viewModel.CloseRequested += (_, saved) => closed = saved;
        viewModel.Theme = ThemePreference.Light;
        viewModel.QuickCaptureHotkey = "Ctrl+Alt+F10";
        viewModel.MatchThresholdText = "0,9";
        viewModel.SearchMarginText = "75";

        viewModel.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(ThemePreference.Light, _store.Saved!.Theme);
        Assert.Equal("Ctrl+Alt+F10", _store.Saved.QuickCaptureHotkey);
        Assert.Null(_store.Saved.DataFolder);
        Assert.Equal(0.9, _profile.Defaults.MatchThreshold);
        Assert.Equal(0.75, _profile.Defaults.SearchMargin);
        Assert.Empty(_dialogs.Informations);
    }

    [Fact]
    public void Invalid_defaults_are_reported_and_nothing_is_saved()
    {
        var viewModel = NewViewModel(_profile);
        viewModel.MatchThresholdText = "1.5";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("The match threshold must be between 0 and 1.", viewModel.ErrorMessage);
        Assert.Null(_store.Saved);
        Assert.Equal(0.75, _profile.Defaults.MatchThreshold);
    }

    [Fact]
    public void Changing_the_data_folder_explains_when_it_applies()
    {
        var viewModel = NewViewModel(null);
        _dialogs.FolderPath = "/new/folder";

        viewModel.BrowseDataFolderCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("/new/folder", _store.Saved!.DataFolder);
        Assert.Contains("next time AutoPlay starts", Assert.Single(_dialogs.Informations));
    }

    [Fact]
    public void Without_profile_only_the_application_settings_are_saved()
    {
        var viewModel = NewViewModel(null);
        viewModel.MatchThresholdText = "not a number";

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.HasProfile);
        Assert.NotNull(_store.Saved);
    }

    [Fact]
    public void ResetDefaults_restores_the_initial_values()
    {
        var viewModel = NewViewModel(_profile);

        viewModel.ResetDefaultsCommand.Execute(null);

        Assert.Equal(0.8, double.Parse(viewModel.MatchThresholdText.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("10000", viewModel.VerificationTimeoutText);
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public AppSettings? Saved { get; set; }

        public AppSettings Load() => Saved ?? new AppSettings();

        public void Save(AppSettings settings) => Saved = settings;
    }
}
