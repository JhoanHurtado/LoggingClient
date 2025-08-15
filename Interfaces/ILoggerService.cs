using System;
using System.Threading.Tasks;

namespace LoggingClient.Interfaces
{
    public interface ILoggerService
    {
        Task LogInfoAsync(string message);
        Task LogErrorAsync(string message, Exception? ex = null);
    }
}