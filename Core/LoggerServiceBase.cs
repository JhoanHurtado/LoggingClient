using System;
using System.Text.Json;
using System.Threading.Tasks;
using LoggingClient.Interfaces;
using LoggingClient.Models;

namespace LoggingClient.Core
{
    public abstract class LoggerServiceBase : ILoggerService
    {
        public string DefaultOrigin { get; set; } = AppDomain.CurrentDomain.FriendlyName;
        public bool UseJsonFormat { get; set; } = true;
        public TimeZoneInfo? TimeZone { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public abstract Task LogAsync(LogEntry entry);

        protected virtual string FormatEntry(LogEntry entry)
        {
            if (TimeZone != null)
            {
                entry.LocalTime = LoggingClient.Utils.TimeZoneHelper.FormatLocalTime(entry.Timestamp, TimeZone);
            }

            if (UseJsonFormat)
            {
                // Si el mensaje ya es un objeto o arreglo JSON válido serializado previamente, se envía directo sin doble escapado
                var trimmed = entry.Message?.Trim();
                if (!string.IsNullOrEmpty(trimmed) &&
                    entry.Data == null &&
                    entry.Exception == null &&
                    ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
                     (trimmed.StartsWith('[') && trimmed.EndsWith(']'))) &&
                    IsValidJson(trimmed))
                {
                    if (trimmed.StartsWith('{') && !trimmed.Contains("localTime", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains("colombiaTime", StringComparison.OrdinalIgnoreCase))
                    {
                        var timeJson = $"\"localTime\":\"{entry.LocalTime}\",\"colombiaTime\":\"{entry.ColombiaTime}\",";
                        return "{" + timeJson + trimmed.Substring(1);
                    }
                    return trimmed;
                }

                return JsonSerializer.Serialize(entry, JsonOptions);
            }

            var dataPart = entry.Data != null ? $" | Data: {JsonSerializer.Serialize(entry.Data, JsonOptions)}" : string.Empty;
            var exPart = entry.Exception != null ? $" | Ex: {entry.Exception.Type}: {entry.Exception.Message} {entry.Exception.StackTrace}" : string.Empty;
            return $"[{entry.LocalTime}] [{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC] [{entry.Level.ToString().ToUpperInvariant()}] [{entry.Origin}] {entry.Message}{dataPart}{exPart}";
        }

        private static bool IsValidJson(string input)
        {
            try
            {
                using var doc = JsonDocument.Parse(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task LogDebugAsync(string message, object? data = null) =>
            LogAsync(CreateEntry(LogLevelType.Debug, message, null, data));

        public Task LogInfoAsync(string message, object? data = null) =>
            LogAsync(CreateEntry(LogLevelType.Info, message, null, data));

        public Task LogWarningAsync(string message, object? data = null) =>
            LogAsync(CreateEntry(LogLevelType.Warning, message, null, data));

        public Task LogErrorAsync(string message, Exception? ex = null, object? data = null) =>
            LogAsync(CreateEntry(LogLevelType.Error, message, ex, data));

        public Task LogCriticalAsync(string message, Exception? ex = null, object? data = null) =>
            LogAsync(CreateEntry(LogLevelType.Critical, message, ex, data));

        protected LogEntry CreateEntry(LogLevelType level, string message, Exception? ex = null, object? data = null)
        {
            return new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Origin = DefaultOrigin,
                Message = message,
                Data = data,
                Exception = ExceptionDetail.FromException(ex)
            };
        }
    }
}
