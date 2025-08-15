using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LoggingClient.CloudWatch
{
    public class CloudWatchLoggerProvider : ILoggerProvider
    {
        private readonly CloudWatchLoggerService _cloudWatchLogService;
        private readonly ConcurrentDictionary<string, CloudWatchLogger> _loggers = new();

        public CloudWatchLoggerProvider(CloudWatchLoggerService cloudWatchLogService)
        {
            _cloudWatchLogService = cloudWatchLogService ?? throw new ArgumentNullException(nameof(cloudWatchLogService));
        }

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new CloudWatchLogger(name, _cloudWatchLogService));

        public void Dispose() => _loggers.Clear();

        private class CloudWatchLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly CloudWatchLoggerService _cloudWatchLogService;

            public CloudWatchLogger(string categoryName, CloudWatchLoggerService cloudWatchLogService)
            {
                _categoryName = categoryName;
                _cloudWatchLogService = cloudWatchLogService;
            }

            public IDisposable? BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                Task.Run(() => _cloudWatchLogService.LogInfoAsync($"[{logLevel}] {_categoryName}: {message}"));
            }
        }
    }
}