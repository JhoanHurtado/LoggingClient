using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LoggingClient.Core;
using LoggingClient.Interfaces;
using LoggingClient.Models;

namespace LoggingClient
{
    /// <summary>
    /// Orquestador que envía cada evento de log a múltiples fuentes en paralelo de forma segura.
    /// </summary>
    public class CompositeLoggerService : LoggerServiceBase, IDisposable
    {
        private readonly List<ILoggerService> _loggers = new();

        public IReadOnlyList<ILoggerService> Providers => _loggers.AsReadOnly();

        public CompositeLoggerService(params ILoggerService[] loggers)
        {
            if (loggers != null)
            {
                _loggers.AddRange(loggers.Where(l => l != null));
            }
        }

        public CompositeLoggerService(IEnumerable<ILoggerService> loggers)
        {
            if (loggers != null)
            {
                _loggers.AddRange(loggers.Where(l => l != null));
            }
        }

        public CompositeLoggerService AddProvider(ILoggerService logger)
        {
            if (logger != null)
            {
                _loggers.Add(logger);
            }
            return this;
        }

        public override async Task LogAsync(LogEntry entry)
        {
            if (_loggers.Count == 0) return;

            var tasks = _loggers.Select(async logger =>
            {
                try
                {
                    await logger.LogAsync(entry).ConfigureAwait(false);
                }
                catch
                {
                    // Aísla fallos individuales para que una fuente caída no detenga las demás
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public void Dispose()
        {
            foreach (var logger in _loggers.OfType<IDisposable>())
            {
                logger.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
