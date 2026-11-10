using System;
using System.Threading.Tasks;
using Amazon;

namespace LoggingClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 1. Configura múltiples fuentes en una sola cadena fluida (Consola + Archivo Local + CloudWatch)
            var logger = LogClientBuilder.Create("OrderService")
                .AddConsole()
                .AddLocalFile("logs/test-log.txt")
                .AddCloudWatch("TestLogGroup", region: RegionEndpoint.USEast1)
                .Build();

            // 2. Envía logs a TODAS las fuentes simultáneamente con una sola llamada (JSON estructurado automático)
            await logger.LogInfoAsync("Servicio iniciado correctamente", new { Environment = "Production", Version = "2.0.0" });
            await logger.LogWarningAsync("Uso de memoria elevado", new { MemoryMb = 850 });
            await logger.LogErrorAsync("Error procesando el pago", new InvalidOperationException("Fondos insuficientes"), new { OrderId = 1024 });

            Console.WriteLine("Logs enviados correctamente a todas las fuentes configuradas.");
        }
    }
}
