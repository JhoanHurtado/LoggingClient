using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using LoggingClient.CloudWatch;
using LoggingClient.Local;
using LoggingClient.Interfaces;
using Microsoft.Extensions.Logging;

namespace LoggingClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Configura AWS CloudWatch client
            var cloudWatchClient = new AmazonCloudWatchLogsClient(RegionEndpoint.USEast1);

            // Instancia del logger de CloudWatch
            var cloudWatchLogger = new CloudWatchLoggerService(
                cloudWatchClient,
                new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<CloudWatchLoggerService>(),
                "TestLogGroup",
                $"TestLogStream-{DateTime.UtcNow:yyyyMMddHHmmss}"
            );

            await cloudWatchLogger.InitializeAsync();

            // Instancia del logger local
            var localLogger = new LocalLoggerService("logs/test-log.txt");

            // Prueba de logs
            await cloudWatchLogger.LogInfoAsync("Este es un mensaje de prueba en CloudWatch.");
            await cloudWatchLogger.LogErrorAsync("Este es un error de prueba en CloudWatch.", new Exception("Excepción de prueba"));

            await localLogger.LogInfoAsync("Este es un mensaje de prueba en archivo local.");
            await localLogger.LogErrorAsync("Este es un error de prueba en archivo local.", new Exception("Excepción de prueba"));

            Console.WriteLine("Logs enviados correctamente. Revisa CloudWatch y el archivo local.");
        }
    }
}