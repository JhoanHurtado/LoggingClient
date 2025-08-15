# LoggingClient

**LoggingClient** es una librería en .NET 8 que permite enviar logs tanto a **AWS CloudWatch** como a archivos locales de texto.  
Soporta formato plano o JSON, lo que facilita la integración con **CloudWatch Logs Insights** para consultas avanzadas.

## 🚀 Características

- Registro de logs en **AWS CloudWatch Logs**.
- Registro de logs en **archivos locales**.
- Formato **plano** o **JSON estructurado**.
- Soporte para niveles de log (`INFO`, `ERROR`).
- Incluye información de **timestamp**, **origen** y **detalles de excepción**.
- Integración sencilla con `ILogger` de .NET.

---

## 📦 Instalación

1. Agrega las referencias necesarias en tu `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="AWSSDK.CloudWatchLogs" Version="4.0.7.4" />
  <PackageReference Include="AWSSDK.Core" Version="4.0.0.22" />
  <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.8" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.8" />
</ItemGroup>
```

2. Configura tus credenciales de AWS en el archivo `~/.aws/credentials` o usando variables de entorno:

```ini
[default]
aws_access_key_id=TU_ACCESS_KEY
aws_secret_access_key=TU_SECRET_KEY
region=us-east-1
```

---

## ⚙️ Configuración

### 1️⃣ Logger a AWS CloudWatch

```csharp
using Amazon;
using Amazon.CloudWatchLogs;
using Microsoft.Extensions.Logging;
using LoggingClient.CloudWatch;

var client = new AmazonCloudWatchLogsClient(RegionEndpoint.USEast1);

var cloudWatchLoggerService = new CloudWatchLoggerService(
    client,
    LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CloudWatchLoggerService>(),
    "MiLogGroup",
    "MiLogStream"
);

await cloudWatchLoggerService.InitializeAsync();
await cloudWatchLoggerService.LogInfoAsync("Servicio iniciado correctamente");
await cloudWatchLoggerService.LogErrorAsync("Error procesando solicitud", new Exception("Detalles del error"));
```

---

### 2️⃣ Logger local

```csharp
using LoggingClient.Local;

var localLogger = new LocalLoggerService("logs/test-log.txt");

await localLogger.LogInfoAsync("Aplicación iniciada");
await localLogger.LogErrorAsync("Fallo en la operación X", new Exception("Algo salió mal"));
```

---

## 📄 Ejemplo de uso en JSON

Para guardar los logs con formato JSON estructurado:

```csharp
var logJson = System.Text.Json.JsonSerializer.Serialize(new
{
    Timestamp = DateTime.UtcNow.ToString("o"),
    Level = "INFO",
    Origin = "NotificationService",
    Message = "Proceso completado",
    Exception = (string)null
});

await cloudWatchLoggerService.LogInfoAsync(logJson);
```

Ejemplo de log resultante en CloudWatch:
```json
{"Timestamp":"2025-08-14T15:30:00Z","Level":"INFO","Origin":"NotificationService","Message":"Proceso completado","Exception":null}
```

> **Nota:** CloudWatch almacena el JSON como texto plano, pero si cada línea es un JSON válido, podrás consultarlo fácilmente con **Logs Insights**.

---

## 🔍 Consultas en CloudWatch Logs Insights

Ejemplo de consulta para filtrar errores:
```sql
fields @timestamp, @message
| filter Level = "ERROR"
| sort @timestamp desc
```

Ejemplo para buscar por servicio de origen:
```sql
fields @timestamp, @message
| filter Origin = "NotificationService"
```

---

## 🛠 Ejemplo de proyecto de prueba (`Program.cs`)

```csharp
using Amazon;
using Amazon.CloudWatchLogs;
using Microsoft.Extensions.Logging;
using LoggingClient.CloudWatch;
using LoggingClient.Local;

class Program
{
    static async Task Main(string[] args)
    {
        // Logger local
        var localLogger = new LocalLoggerService("logs/test-log.txt");
        await localLogger.LogInfoAsync("Iniciando prueba local");

        // Logger AWS CloudWatch
        var client = new AmazonCloudWatchLogsClient(RegionEndpoint.USEast1);
        var cloudLogger = new CloudWatchLoggerService(
            client,
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CloudWatchLoggerService>(),
            "TestLogGroup",
            "TestLogStream"
        );

        await cloudLogger.InitializeAsync();

        var logJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Level = "INFO",
            Origin = "TestApp",
            Message = "Prueba de log JSON",
            Exception = (string)null
        });

        await cloudLogger.LogInfoAsync(logJson);
    }
}
```

---

## 🏗 Compilación en Release

Para compilar en modo release:

```bash
dotnet clean
dotnet restore
dotnet build -c Release
```

El binario se generará en:
```
bin/Release/net8.0/
```

---

## 📌 Buenas prácticas

- Mantén el nombre de `LogGroup` y `LogStream` consistentes para un servicio.
- Usa JSON en una sola línea para que CloudWatch pueda parsearlo.
- No envíes objetos muy grandes; CloudWatch tiene límites de tamaño (256 KB por evento).
- Usa `await` siempre que sea posible para evitar pérdida de logs.

---
