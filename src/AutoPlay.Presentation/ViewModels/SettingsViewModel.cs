using System.Globalization;
using AutoPlay.Application.Settings;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPlay.Presentation.ViewModels;

/// <summary>Edits the application settings and the default execution settings of the selected profile.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ProfileService _profileService;
    private readonly IUserDialogs _dialogs;
    private readonly Profile? _profile;
    private readonly string? _initialDataFolder;

    /// <param name="profile">The selected profile, whose defaults can be edited; null if none.</param>
    /// <param name="defaultDataFolder">The data folder used when none is set.</param>
    public SettingsViewModel(
        SettingsService settingsService,
        ProfileService profileService,
        IUserDialogs dialogs,
        Profile? profile,
        string defaultDataFolder)
    {
        _settingsService = settingsService;
        _profileService = profileService;
        _dialogs = dialogs;
        _profile = profile;
        DefaultDataFolder = defaultDataFolder;

        var settings = settingsService.Current;
        Theme = settings.Theme;
        StopWithEscape = settings.StopWithEscape;
        QuickCaptureEnabled = settings.QuickCaptureEnabled;
        QuickCaptureHotkey = settings.QuickCaptureHotkey;
        DataFolder = settings.DataFolder ?? string.Empty;
        _initialDataFolder = settings.DataFolder;

        LoadDefaults(profile?.Defaults ?? new ProfileDefaults());
    }

    /// <summary>Raised when the window must close; the argument tells whether the settings were saved.</summary>
    public event EventHandler<bool>? CloseRequested;

    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();

    public string DefaultDataFolder { get; }

    public bool HasProfile => _profile is not null;

    public string ProfileTitle => _profile is null
        ? "Profile defaults (select a profile in the main window to edit them)"
        : $"Defaults of the profile '{_profile.Name}'";

    [ObservableProperty]
    public partial ThemePreference Theme { get; set; }

    [ObservableProperty]
    public partial bool StopWithEscape { get; set; }

    [ObservableProperty]
    public partial bool QuickCaptureEnabled { get; set; }

    /// <summary>Shortcut of the quick capture, e.g. "Ctrl+Shift+F9" (set by pressing the keys in the view).</summary>
    [ObservableProperty]
    public partial string QuickCaptureHotkey { get; set; }

    /// <summary>Folder of the profiles; empty for the default one.</summary>
    [ObservableProperty]
    public partial string DataFolder { get; set; }

    [ObservableProperty]
    public partial string MatchThresholdText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VerificationTimeoutText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RetryIntervalText { get; set; } = string.Empty;

    /// <summary>Search margin, in percent of the location size.</summary>
    [ObservableProperty]
    public partial string SearchMarginText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TakeoverToleranceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private void BrowseDataFolder()
    {
        if (_dialogs.AskFolderPath("Folder of the AutoPlay profiles") is { } folder)
        {
            DataFolder = folder;
        }
    }

    [RelayCommand]
    private void UseDefaultDataFolder() => DataFolder = string.Empty;

    [RelayCommand]
    private void ResetDefaults() => LoadDefaults(new ProfileDefaults());

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = null;
        ProfileDefaults? defaults = null;
        if (_profile is not null)
        {
            if (TryParseDefaults(out var parsed) is { } parseError)
            {
                ErrorMessage = parseError;
                return;
            }

            defaults = parsed;
        }

        var settings = new AppSettings
        {
            Theme = Theme,
            StopWithEscape = StopWithEscape,
            QuickCaptureEnabled = QuickCaptureEnabled,
            QuickCaptureHotkey = QuickCaptureHotkey.Trim(),
            DataFolder = string.IsNullOrWhiteSpace(DataFolder) ? null : DataFolder,
        };

        // Profile defaults first: they are validated before anything is saved.
        if (_profile is not null && defaults is not null)
        {
            var defaultsResult = _profileService.UpdateDefaults(_profile, defaults);
            if (!defaultsResult.Succeeded)
            {
                ErrorMessage = string.Join(Environment.NewLine, defaultsResult.Errors);
                return;
            }
        }

        var result = _settingsService.Save(settings);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(Environment.NewLine, result.Errors);
            return;
        }

        if (!string.Equals(settings.DataFolder, _initialDataFolder, StringComparison.OrdinalIgnoreCase))
        {
            _dialogs.ShowInformation(
                "The new data folder will be used the next time AutoPlay starts. "
                + "Existing profiles are not moved: copy them to the new folder if you want to keep them.");
        }

        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    private void LoadDefaults(ProfileDefaults defaults)
    {
        MatchThresholdText = defaults.MatchThreshold.ToString(CultureInfo.CurrentCulture);
        VerificationTimeoutText = defaults.VerificationTimeoutMs.ToString(CultureInfo.CurrentCulture);
        RetryIntervalText = defaults.RetryIntervalMs.ToString(CultureInfo.CurrentCulture);
        SearchMarginText = (defaults.SearchMargin * 100).ToString(CultureInfo.CurrentCulture);
        TakeoverToleranceText = defaults.UserTakeoverTolerancePx.ToString(CultureInfo.CurrentCulture);
    }

    private string? TryParseDefaults(out ProfileDefaults defaults)
    {
        defaults = new ProfileDefaults();
        if (!TryParseNumber(MatchThresholdText, out var threshold))
        {
            return "The match threshold must be a number between 0 and 1.";
        }

        if (!int.TryParse(VerificationTimeoutText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var timeout)
            || !int.TryParse(RetryIntervalText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var retry)
            || !int.TryParse(TakeoverToleranceText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var tolerance))
        {
            return "The timeout, retry interval and mouse tolerance must be whole numbers.";
        }

        if (!TryParseNumber(SearchMarginText, out var marginPercent))
        {
            return "The search margin must be a number (percent).";
        }

        defaults = new ProfileDefaults
        {
            MatchThreshold = threshold,
            VerificationTimeoutMs = timeout,
            RetryIntervalMs = retry,
            SearchMargin = marginPercent / 100,
            UserTakeoverTolerancePx = tolerance,
        };
        return null;
    }

    /// <summary>Parses a number written with a dot or a comma.</summary>
    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
