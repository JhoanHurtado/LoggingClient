using System;
using System.Threading.Tasks;
using LoggingClient.Models;

namespace LoggingClient.Interfaces
{
    public interface ILoggerService
    {
        Task LogAsync(LogEntry entry);
        Task LogDebugAsync(string message, object? data = null);
        Task LogInfoAsync(string message, object? data = null);
        Task LogWarningAsync(string message, object? data = null);
        Task LogErrorAsync(string message, Exception? ex = null, object? data = null);
        Task LogCriticalAsync(string message, Exception? ex = null, object? data = null);
    }
}
