using AutoPlay.Application.Settings;

namespace AutoPlay.Application.Ports;

/// <summary>Driven port: persistence of the application settings.</summary>
public interface ISettingsStore
{
    /// <summary>Returns the saved settings, or the default settings if none were saved.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
