using System;
using System.Threading.Tasks;
using Amazon;
using LoggingClient.Interfaces;
using LoggingClient.Models;

namespace LoggingClient
{
    /// <summary>
    /// Fachada estática opcional para usar el logger globalmente con 1 sola línea de código.
    /// </summary>
    public static class LogClient
    {
        private static ILoggerService _instance = LogClientBuilder.Create()
            .AddConsole()
            .Build();

        public static ILoggerService Current => _instance;

        /// <summary>
        /// Configura las fuentes de log globales usando el builder fluido.
        /// </summary>
        public static ILoggerService Configure(Action<LogClientBuilder> configure)
        {
            var builder = LogClientBuilder.Create();
            configure(builder);
            _instance = builder.Build();
            return _instance;
        }

        public static Task LogAsync(LogEntry entry) => _instance.LogAsync(entry);
        public static Task DebugAsync(string message, object? data = null) => _instance.LogDebugAsync(message, data);
        public static Task InfoAsync(string message, object? data = null) => _instance.LogInfoAsync(message, data);
        public static Task WarningAsync(string message, object? data = null) => _instance.LogWarningAsync(message, data);
        public static Task ErrorAsync(string message, Exception? ex = null, object? data = null) => _instance.LogErrorAsync(message, ex, data);
        public static Task CriticalAsync(string message, Exception? ex = null, object? data = null) => _instance.LogCriticalAsync(message, ex, data);
    }
}
