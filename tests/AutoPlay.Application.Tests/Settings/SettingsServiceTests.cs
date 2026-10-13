using AutoPlay.Application.Ports;
using AutoPlay.Application.Settings;

namespace AutoPlay.Application.Tests.Settings;

public class SettingsServiceTests
{
    private readonly MemorySettingsStore _store = new();
    private readonly SettingsService _service;

    public SettingsServiceTests() => _service = new SettingsService(_store);

    [Fact]
    public void Current_returns_the_saved_settings_or_the_defaults()
    {
        Assert.Equal(ThemePreference.System, _service.Current.Theme);

        _store.Failure = new PersistenceException("unreadable");
        Assert.Equal(AppSettings.DefaultQuickCaptureHotkey, new SettingsService(_store).Current.QuickCaptureHotkey);
    }

    [Fact]
    public void Save_stores_and_notifies()
    {
        AppSettings? notified = null;
        _service.Changed += (_, settings) => notified = settings;
        // A fully qualified path on the OS running the tests (C:\... on Windows, /... elsewhere).
        var folder = Path.Combine(Path.GetTempPath(), "autoplay");
        var settings = new AppSettings { Theme = ThemePreference.Dark, DataFolder = $" {folder} " };

        var result = _service.Save(settings);

        Assert.True(result.Succeeded);
        Assert.Same(settings, _store.Saved);
        Assert.Same(settings, notified);
        Assert.Same(settings, _service.Current);
        Assert.Equal(folder, settings.DataFolder);
    }

    [Fact]
    public void Save_validates_the_hotkey_and_the_data_folder()
    {
        var result = _service.Save(new AppSettings { QuickCaptureHotkey = " ", DataFolder = "relative" });

        Assert.Equal(
            ["Choose a shortcut for the quick capture, or disable it.", "The data folder must be a full path, for example D:\\AutoPlay."],
            result.Errors);
        Assert.Null(_store.Saved);
    }

    [Fact]
    public void A_disabled_quick_capture_needs_no_hotkey()
    {
        Assert.True(_service.Save(new AppSettings { QuickCaptureEnabled = false, QuickCaptureHotkey = "" }).Succeeded);
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        public AppSettings? Saved { get; private set; }

        public PersistenceException? Failure { get; set; }

        public AppSettings Load() => Failure is { } failure ? throw failure : Saved ?? new AppSettings();

        public void Save(AppSettings settings) => Saved = settings;
    }
}
