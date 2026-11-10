using System;
using System.Threading.Tasks;
using LoggingClient.Core;
using LoggingClient.Models;

namespace LoggingClient.ConsoleLog
{
    public class ConsoleLoggerService : LoggerServiceBase
    {
        private readonly bool _useColors;
        private readonly object _lock = new();

        public ConsoleLoggerService(bool useJsonFormat = false, bool useColors = true, string? origin = null)
        {
            UseJsonFormat = useJsonFormat;
            _useColors = useColors;
            if (!string.IsNullOrWhiteSpace(origin))
                DefaultOrigin = origin;
        }

        public override Task LogAsync(LogEntry entry)
        {
            var formattedMessage = FormatEntry(entry);

            lock (_lock)
            {
                if (_useColors)
                {
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = GetColor(entry.Level);
                    Console.WriteLine(formattedMessage);
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    Console.WriteLine(formattedMessage);
                }
            }

            return Task.CompletedTask;
        }

        private static ConsoleColor GetColor(LogLevelType level) => level switch
        {
            LogLevelType.Debug => ConsoleColor.Gray,
            LogLevelType.Info => ConsoleColor.Cyan,
            LogLevelType.Warning => ConsoleColor.Yellow,
            LogLevelType.Error => ConsoleColor.Red,
            LogLevelType.Critical => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };
    }
}
