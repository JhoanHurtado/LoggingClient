using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LoggingClient.Models
{
    public enum LogLevelType
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Hora local de generación según la zona horaria parametrizada (por defecto Colombia UTC-5).
        /// Permite visualizar inmediatamente en CloudWatch la hora local sin desfases por la zona del servidor.
        /// </summary>
        public string LocalTime { get; set; } = LoggingClient.Utils.TimeZoneHelper.FormatLocalTime();

        /// <summary>
        /// Alias de compatibilidad hacia atrás para ColombiaTime.
        /// </summary>
        public string ColombiaTime
        {
            get => LocalTime;
            set => LocalTime = value;
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LogLevelType Level { get; set; } = LogLevelType.Info;

        public string Origin { get; set; } = AppDomain.CurrentDomain.FriendlyName;

        public string Message { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Data { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ExceptionDetail? Exception { get; set; }
    }

    public class ExceptionDetail
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? InnerException { get; set; }

        public static ExceptionDetail? FromException(Exception? ex)
        {
            if (ex == null) return null;
            return new ExceptionDetail
            {
                Type = ex.GetType().FullName ?? ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.ToString()
            };
        }
    }
}
