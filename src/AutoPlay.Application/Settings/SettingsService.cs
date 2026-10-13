using AutoPlay.Application.Ports;
using AutoPlay.Application.UseCases;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoPlay.Application.Settings;

/// <summary>Use cases on the application settings.</summary>
public sealed class SettingsService(ISettingsStore store, ILogger<SettingsService>? logger = null)
{
    private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;
    private AppSettings? _current;

    /// <summary>Raised after the settings were saved, with the new settings.</summary>
    public event EventHandler<AppSettings>? Changed;

    /// <summary>The current settings; the defaults if they cannot be read.</summary>
    public AppSettings Current => _current ??= LoadOrDefault();

    public OperationResult Save(AppSettings settings)
    {
        var errors = new List<string>();
        if (settings.QuickCaptureEnabled && string.IsNullOrWhiteSpace(settings.QuickCaptureHotkey))
        {
            errors.Add("Choose a shortcut for the quick capture, or disable it.");
        }

        if (settings.DataFolder is { } folder && (folder.Trim().Length == 0 || !Path.IsPathFullyQualified(folder.Trim())))
        {
            errors.Add("The data folder must be a full path, for example D:\\AutoPlay.");
        }

        if (errors.Count > 0)
        {
            return OperationResult.Failure(errors);
        }

        settings.DataFolder = settings.DataFolder?.Trim();
        try
        {
            store.Save(settings);
        }
        catch (PersistenceException ex)
        {
            SettingsLog.StorageError(_logger, ex);
            return OperationResult.Failure(ex.Message);
        }

        _current = settings;
        Changed?.Invoke(this, settings);
        return OperationResult.Success();
    }

    private AppSettings LoadOrDefault()
    {
        try
        {
            return store.Load();
        }
        catch (PersistenceException ex)
        {
            SettingsLog.StorageError(_logger, ex);
            return new AppSettings();
        }
    }
}

internal static partial class SettingsLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings could not be read or saved")]
    public static partial void StorageError(ILogger logger, PersistenceException exception);
}
