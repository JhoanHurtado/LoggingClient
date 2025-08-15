using System;
using System.IO;
using System.Threading.Tasks;
using LoggingClient.Interfaces;

namespace LoggingClient.Local
{
    public class LocalLoggerService : ILoggerService
    {
        private readonly string _filePath;

        public LocalLoggerService(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public Task LogInfoAsync(string message) => LogAsync($"INFO: {DateTime.UtcNow}: {message}");
        public Task LogErrorAsync(string message, Exception? ex = null) => LogAsync($"ERROR: {DateTime.UtcNow}: {message} {ex?.ToString()}");

        private Task LogAsync(string message)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.AppendAllText(_filePath, message + Environment.NewLine);
            return Task.CompletedTask;
        }
    }
}