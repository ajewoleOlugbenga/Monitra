namespace Monitra.Core.Enums;

// Named DeviceLogLevel (not LogLevel) to avoid colliding with Microsoft.Extensions.Logging.LogLevel
// wherever this and the framework's own logging are both in scope.
public enum DeviceLogLevel
{
    Info,
    Warning,
    Error
}
