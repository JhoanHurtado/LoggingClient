using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LoggingClient.Core;
using LoggingClient.Models;

namespace LoggingClient.Local
{
    public class LocalLoggerService : LoggerServiceBase
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public LocalLoggerService(string filePath = "logs/app.log", bool useJsonFormat = true, string? origin = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            _filePath = filePath;
            UseJsonFormat = useJsonFormat;
            if (!string.IsNullOrWhiteSpace(origin))
                DefaultOrigin = origin;
        }

        public override async Task LogAsync(LogEntry entry)
        {
            var formattedMessage = FormatEntry(entry);

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.AppendAllTextAsync(_filePath, formattedMessage + Environment.NewLine).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
