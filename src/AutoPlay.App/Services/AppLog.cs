using Microsoft.Extensions.Logging;

namespace AutoPlay.App.Services;

/// <summary>Log messages of the application lifetime.</summary>
internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "AutoPlay {Version} started; data in {Root}")]
    public static partial void Started(ILogger logger, string version, string root);

    [LoggerMessage(Level = LogLevel.Information, Message = "AutoPlay stopped")]
    public static partial void Stopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduler: {Message}")]
    public static partial void Scheduler(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error")]
    public static partial void Unexpected(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Fatal error")]
    public static partial void Crashed(ILogger logger, Exception? exception);
}
