using AutoPlay.Application.Ports;
using Microsoft.Extensions.Logging;

namespace AutoPlay.Application.UseCases;

/// <summary>Log messages of the use cases.</summary>
internal static partial class UseCaseLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Storage error")]
    public static partial void StorageError(ILogger logger, PersistenceException exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Quick capture added to the profile '{Profile}'")]
    public static partial void QuickCaptured(ILogger logger, string profile);
}
