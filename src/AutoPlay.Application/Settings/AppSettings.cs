namespace AutoPlay.Application.Settings;

/// <summary>Color theme of the user interface.</summary>
public enum ThemePreference
{
    /// <summary>Follows the Windows setting (light or dark).</summary>
    System,
    Light,
    Dark,
}

/// <summary>Settings of the application (shared by all profiles).</summary>
public sealed class AppSettings
{
    /// <summary>Default shortcut of the quick capture.</summary>
    public const string DefaultQuickCaptureHotkey = "Ctrl+Shift+F9";

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Whether Esc stops a run (in addition to F12, which always does).</summary>
    public bool StopWithEscape { get; set; } = true;

    /// <summary>Whether the quick capture shortcut is active while AutoPlay is running.</summary>
    public bool QuickCaptureEnabled { get; set; } = true;

    /// <summary>Shortcut of the quick capture, e.g. "Ctrl+Shift+F9".</summary>
    public string QuickCaptureHotkey { get; set; } = DefaultQuickCaptureHotkey;

    /// <summary>Folder of the profiles; null for the default one. Taken into account at the next start.</summary>
    public string? DataFolder { get; set; }
}
