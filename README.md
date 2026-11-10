# LoggingClient (`ManagementLoggingClient`)

**LoggingClient** es una librería en **.NET 8 y .NET 10 (LTS)** diseñada para que el registro de logs sea **extremadamente sencillo**, permitiendo enviar eventos a **múltiples fuentes simultáneamente** (**AWS CloudWatch**, **archivos locales**, **consola** o **fuentes personalizadas**) con una sola llamada y el mínimo código posible.

Formatea automáticamente tus mensajes, metadatos y excepciones en **JSON estructurado** listo para **CloudWatch Logs Insights** (o texto plano si lo prefieres), y se encarga de inicializar grupos/streams de CloudWatch de forma automática (*lazy initialization*).

---

## 🚀 Características Principales

- **Multi-fuente con 1 sola llamada:** Envía el mismo log en paralelo a Consola, Archivo Local y AWS CloudWatch usando `LogClientBuilder`.
- **Cero configuración repetitiva (*Zero Boilerplate*):** Ya no necesitas instanciar `AmazonCloudWatchLogsClient`, ni `LoggerFactory`, ni llamar a `InitializeAsync()` manualmente, ni serializar JSONs a mano.
- **JSON estructurado automático:** Incluye `timestamp`, `level`, `origin`, `message`, objeto `data` arbitrario y desglose detallado de `exception`.
- **Niveles completos:** `Debug`, `Info`, `Warning`, `Error` y `Critical`.
- **Inyección de Dependencias (DI):** Integración en 1 línea con `IServiceCollection` e `ILoggingBuilder` de .NET.
- **Thread-Safe y Resiliente:** Escritura segura en archivos concurrentes (`SemaphoreSlim`), aislamiento de fallos entre fuentes y recuperación automática de `SequenceToken` en CloudWatch.

---

## 📦 Instalación y Requisitos

1. Instala el paquete NuGet `ManagementLoggingClient` o agrega las referencias en tu `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="AWSSDK.CloudWatchLogs" Version="4.0.17.2" />
  <PackageReference Include="AWSSDK.Core" Version="4.0.3.26" />
  <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.8" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.8" />
</ItemGroup>
```

2. Si usas **AWS CloudWatch**, configura tus credenciales en `~/.aws/credentials`, variables de entorno (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`) o pásalas directamente en `.AddCloudWatch(...)`:

```ini
[default]
aws_access_key_id=TU_ACCESS_KEY
aws_secret_access_key=TU_SECRET_KEY
region=us-east-1
```

---

## ⚡ Guía de Uso Rápido

### 1️⃣ Usando el Builder Fluido (`LogClientBuilder`)

Configura todas las fuentes que necesites encadenando métodos y registra en todas a la vez:

```csharp
using Amazon;
using LoggingClient;

var logger = LogClientBuilder.Create("PaymentService")
    .AddConsole()                                                    // Salida en consola con colores
    .AddLocalFile("logs/payments.log")                               // Archivo local (crea la carpeta si no existe)
    .AddCloudWatch("ProductionLogs", region: RegionEndpoint.USEast1) // AWS CloudWatch (auto-inicializado)
    .Build();

// Envía a Consola, Archivo y CloudWatch simultáneamente:
await logger.LogDebugAsync("Iniciando validación de pago");
await logger.LogInfoAsync("Pago procesado", new { OrderId = 9841, Amount = 150.00 });
await logger.LogWarningAsync("Latencia alta en pasarela", new { ElapsedMs = 3200 });
await logger.LogErrorAsync("Fallo al cobrar tarjeta", ex, new { CustomerId = "CUST-01" });
await logger.LogCriticalAsync("Servicio de pagos no disponible", ex);
```

### 2️⃣ Uso Estático Global (`LogClient`)

Si no quieres pasar instancias por constructor, configúralo una sola vez al iniciar tu aplicación y úsalo desde cualquier clase:

```csharp
using LoggingClient;

// 1. Al inicio de tu aplicación (Program.cs):
LogClient.Configure(b => b
    .WithOrigin("AuthService")
    .AddConsole()
    .AddLocalFile("logs/auth.log")
    .AddCloudWatch("AuthLogGroup"));

// 2. En cualquier parte de tu código:
await LogClient.InfoAsync("Usuario autenticado", new { UserId = 42 });
await LogClient.ErrorAsync("Token inválido", ex);
```

### 3️⃣ Con Inyección de Dependencias en ASP.NET Core / Worker Services

Registra `ILoggerService` (y conéctalo automáticamente al `ILogger<T>` estándar de .NET) en tu `Program.cs`:

```csharp
using Amazon;
using LoggingClient;
using LoggingClient.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLoggingClient(log => log
    .WithOrigin("WebApi")
    .AddConsole()
    .AddLocalFile("logs/api.log")
    .AddCloudWatch("ApiLogGroup", region: RegionEndpoint.USEast1));

var app = builder.Build();

app.MapGet("/ping", async (ILoggerService logger) =>
{
    await logger.LogInfoAsync("Ping recibido", new { Ip = "127.0.0.1" });
    return Results.Ok("Pong");
});

app.Run();
```

### 4️⃣ Uso Individual por Proveedor (Opcional)

También puedes instanciar cada servicio directamente de forma simplificada:

```csharp
using Amazon;
using LoggingClient.CloudWatch;
using LoggingClient.Local;
using LoggingClient.ConsoleLog;

// Archivo local
var localLogger = new LocalLoggerService("logs/local.log");
await localLogger.LogInfoAsync("Mensaje en archivo local");

// AWS CloudWatch (sin necesidad de crear AmazonCloudWatchLogsClient ni llamar a InitializeAsync)
using var cwLogger = new CloudWatchLoggerService("MiLogGroup", region: RegionEndpoint.USEast1);
await cwLogger.LogInfoAsync("Mensaje directo a CloudWatch", new { Env = "Prod" });

// Consola
var consoleLogger = new ConsoleLoggerService(useJsonFormat: false, useColors: true);
await consoleLogger.LogWarningAsync("Advertencia en consola");
```

---

## 📄 Formato de Salida (JSON vs. Texto Plano)

### Formato JSON Estructurado (Por defecto en Archivo y CloudWatch)
Genera automáticamente un JSON compacto en una sola línea (ideal para **CloudWatch Logs Insights**):

```json
{"timestamp":"2026-11-09T23:30:00.1234567Z","level":"Error","origin":"PaymentService","message":"Fallo al cobrar tarjeta","data":{"customerId":"CUST-01"},"exception":{"type":"System.InvalidOperationException","message":"Fondos insuficientes","stackTrace":"..."}}
```

### Formato Texto Plano
Si prefieres texto plano globalmente o en un proveedor específico, usa `.UseJson(false)` o pasa `useJsonFormat: false`:

```text
[2026-11-09 23:30:00.123 UTC] [ERROR] [PaymentService] Fallo al cobrar tarjeta | Data: {"customerId":"CUST-01"} | Ex: System.InvalidOperationException: Fondos insuficientes
```

---

## 🔌 Crear una Fuente Personalizada

Agregar un nuevo destino de logs (Base de Datos, Seq, Slack, Webhook, etc.) es tan simple como heredar de `LoggerServiceBase` e implementar `LogAsync`:

```csharp
using System.Threading.Tasks;
using LoggingClient.Core;
using LoggingClient.Models;

public class WebhookLoggerService : LoggerServiceBase
{
    public override async Task LogAsync(LogEntry entry)
    {
        string payload = FormatEntry(entry); // Devuelve el JSON o texto formateado
        // Enviar payload por HttpClient...
        await Task.CompletedTask;
    }
}

// Agrégalo al builder:
var logger = LogClientBuilder.Create("MyService")
    .AddConsole()
    .AddCustom(new WebhookLoggerService())
    .Build();
```

---

## 🔍 Consultas en CloudWatch Logs Insights

Gracias al formato JSON automático, puedes consultar cualquier propiedad o metadato directamente en AWS:

**Filtrar errores por servicio y mostrar datos adjuntos:**
```sql
fields @timestamp, origin, message, data.orderId, exception.type, exception.message
| filter level = "Error" and origin = "PaymentService"
| sort @timestamp desc
```

---

## 🏗 Compilación y Empaquetado NuGet

```bash
dotnet clean
dotnet restore
dotnet build -c Release
```

El binario y el paquete NuGet (`.nupkg`) se generarán en `bin/Release/`.
