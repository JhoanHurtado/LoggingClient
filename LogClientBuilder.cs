using System;
using System.Collections.Generic;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.Runtime;
using LoggingClient.CloudWatch;
using LoggingClient.ConsoleLog;
using LoggingClient.Core;
using LoggingClient.Interfaces;
using LoggingClient.Local;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoggingClient
{
    /// <summary>
    /// Builder fluido para configurar múltiples fuentes de log con el mínimo código posible.
    /// </summary>
    public class LogClientBuilder
    {
        private readonly List<ILoggerService> _providers = new();
        private string _origin = AppDomain.CurrentDomain.FriendlyName;
        private bool _useJsonFormat = true;

        public static LogClientBuilder Create(string? origin = null)
        {
            var builder = new LogClientBuilder();
            if (!string.IsNullOrWhiteSpace(origin))
            {
                builder._origin = origin;
            }
            return builder;
        }

        /// <summary>
        /// Define el nombre del servicio u origen para todos los logs.
        /// </summary>
        public LogClientBuilder WithOrigin(string origin)
        {
            if (!string.IsNullOrWhiteSpace(origin))
            {
                _origin = origin;
                foreach (var provider in _providers)
                {
                    if (provider is LoggerServiceBase baseLogger)
                        baseLogger.DefaultOrigin = origin;
                }
            }
            return this;
        }

        /// <summary>
        /// Define si los logs deben formatearse en JSON estructurado (true por defecto) o texto plano (false).
        /// </summary>
        public LogClientBuilder UseJson(bool useJson = true)
        {
            _useJsonFormat = useJson;
            foreach (var provider in _providers)
            {
                if (provider is LoggerServiceBase baseLogger)
                    baseLogger.UseJsonFormat = useJson;
            }
            return this;
        }

        /// <summary>
        /// Agrega salida de logs por consola.
        /// </summary>
        public LogClientBuilder AddConsole(bool? useJsonFormat = null, bool useColors = true)
        {
            _providers.Add(new ConsoleLoggerService(useJsonFormat ?? false, useColors, _origin));
            return this;
        }

        /// <summary>
        /// Agrega almacenamiento de logs en archivo local.
        /// </summary>
        public LogClientBuilder AddLocalFile(string filePath = "logs/app.log", bool? useJsonFormat = null)
        {
            _providers.Add(new LocalLoggerService(filePath, useJsonFormat ?? _useJsonFormat, _origin));
            return this;
        }

        /// <summary>
        /// Agrega envío de logs a AWS CloudWatch sin requerir instanciar clientes ni llamar a InitializeAsync manualmente.
        /// </summary>
        public LogClientBuilder AddCloudWatch(
            string logGroupName,
            string? logStreamName = null,
            RegionEndpoint? region = null,
            AWSCredentials? credentials = null,
            bool? useJsonFormat = null)
        {
            _providers.Add(new CloudWatchLoggerService(
                logGroupName,
                logStreamName,
                region,
                credentials,
                useJsonFormat ?? _useJsonFormat,
                _origin));
            return this;
        }

        /// <summary>
        /// Agrega envío de logs a AWS CloudWatch utilizando un cliente preconfigurado.
        /// </summary>
        public LogClientBuilder AddCloudWatch(
            IAmazonCloudWatchLogs client,
            string logGroupName,
            string? logStreamName = null,
            bool? useJsonFormat = null)
        {
            _providers.Add(new CloudWatchLoggerService(
                client,
                null,
                logGroupName,
                logStreamName,
                useJsonFormat ?? _useJsonFormat,
                _origin));
            return this;
        }

        /// <summary>
        /// Agrega un proveedor de log personalizado que implemente ILoggerService.
        /// </summary>
        public LogClientBuilder AddCustom(ILoggerService customLogger)
        {
            if (customLogger != null)
            {
                if (customLogger is LoggerServiceBase baseLogger && string.IsNullOrWhiteSpace(baseLogger.DefaultOrigin))
                {
                    baseLogger.DefaultOrigin = _origin;
                }
                _providers.Add(customLogger);
            }
            return this;
        }

        /// <summary>
        /// Construye el servicio unificado de logging que despacha a todas las fuentes configuradas.
        /// </summary>
        public ILoggerService Build()
        {
            return new CompositeLoggerService(_providers)
            {
                DefaultOrigin = _origin,
                UseJsonFormat = _useJsonFormat
            };
        }
    }

    /// <summary>
    /// Extensiones para inyección de dependencias en .NET (IServiceCollection e ILoggingBuilder).
    /// </summary>
    public static class LoggingClientExtensions
    {
        /// <summary>
        /// Registra ILoggerService en el contenedor de dependencias y opcionalmente lo integra con ILogger de .NET.
        /// </summary>
        public static IServiceCollection AddLoggingClient(
            this IServiceCollection services,
            Action<LogClientBuilder> configure,
            bool integrateWithDotNetLogger = true)
        {
            var builder = LogClientBuilder.Create();
            configure(builder);
            var loggerService = builder.Build();

            services.AddSingleton<ILoggerService>(loggerService);

            if (integrateWithDotNetLogger)
            {
                services.AddSingleton<ILoggerProvider>( new CloudWatchLoggerProvider(loggerService));
            }

            return services;
        }

        /// <summary>
        /// Agrega LoggingClient directamente como proveedor de Microsoft.Extensions.Logging.
        /// </summary>
        public static ILoggingBuilder AddLoggingClient(
            this ILoggingBuilder loggingBuilder,
            Action<LogClientBuilder> configure)
        {
            var builder = LogClientBuilder.Create();
            configure(builder);
            var loggerService = builder.Build();

            loggingBuilder.AddProvider(new CloudWatchLoggerProvider(loggerService));
            loggingBuilder.Services.AddSingleton<ILoggerService>(loggerService);
            return loggingBuilder;
        }
    }
}
