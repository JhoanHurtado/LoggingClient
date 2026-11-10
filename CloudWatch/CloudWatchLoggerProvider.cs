using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using LoggingClient.Interfaces;
using LoggingClient.Models;
using Microsoft.Extensions.Logging;

namespace LoggingClient.CloudWatch
{
    public class CloudWatchLoggerProvider : ILoggerProvider
    {
        private readonly ILoggerService _loggerService;
        private readonly ConcurrentDictionary<string, ProviderLogger> _loggers = new();

        public CloudWatchLoggerProvider(ILoggerService loggerService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new ProviderLogger(name, _loggerService));

        public void Dispose()
        {
            _loggers.Clear();
            if (_loggerService is IDisposable disposable)
            {
                disposable.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        private class ProviderLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly ILoggerService _loggerService;

            public ProviderLogger(string categoryName, ILoggerService loggerService)
            {
                _categoryName = categoryName;
                _loggerService = loggerService;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                var message = formatter(state, exception);
                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = MapLevel(logLevel),
                    Origin = _categoryName,
                    Message = message,
                    Exception = ExceptionDetail.FromException(exception)
                };

                _ = Task.Run(() => _loggerService.LogAsync(entry));
            }

            private static LogLevelType MapLevel(LogLevel logLevel) => logLevel switch
            {
                LogLevel.Trace or LogLevel.Debug => LogLevelType.Debug,
                LogLevel.Information => LogLevelType.Info,
                LogLevel.Warning => LogLevelType.Warning,
                LogLevel.Error => LogLevelType.Error,
                LogLevel.Critical => LogLevelType.Critical,
                _ => LogLevelType.Info
            };
        }
    }
}
