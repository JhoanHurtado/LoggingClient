using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using LoggingClient.Core;
using LoggingClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoggingClient.CloudWatch
{
    public class CloudWatchLoggerService : LoggerServiceBase, IDisposable
    {
        private readonly IAmazonCloudWatchLogs _client;
        private readonly ILogger<CloudWatchLoggerService> _logger;
        private readonly string _logGroupName;
        private readonly string _logStreamName;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly bool _ownsClient;
        private string? _sequenceToken;
        private bool _isInitialized;

        /// <summary>
        /// Constructor simplificado: crea el cliente de AWS automáticamente e inicializa de forma perezosa (lazy).
        /// </summary>
        public CloudWatchLoggerService(
            string logGroupName,
            string? logStreamName = null,
            RegionEndpoint? region = null,
            AWSCredentials? credentials = null,
            bool useJsonFormat = true,
            string? origin = null)
        {
            _logGroupName = !string.IsNullOrWhiteSpace(logGroupName)
                ? logGroupName
                : throw new ArgumentNullException(nameof(logGroupName));

            _logStreamName = !string.IsNullOrWhiteSpace(logStreamName)
                ? logStreamName
                : $"{Environment.MachineName}-{DateTime.UtcNow:yyyyMMdd}";

            var targetRegion = region ?? RegionEndpoint.USEast1;
            _client = credentials != null
                ? new AmazonCloudWatchLogsClient(credentials, targetRegion)
                : new AmazonCloudWatchLogsClient(targetRegion);

            _ownsClient = true;
            _logger = NullLogger<CloudWatchLoggerService>.Instance;
            UseJsonFormat = useJsonFormat;
            if (!string.IsNullOrWhiteSpace(origin))
                DefaultOrigin = origin;
        }

        /// <summary>
        /// Constructor avanzado / compatible con versiones anteriores e inyección de dependencias.
        /// </summary>
        public CloudWatchLoggerService(
            IAmazonCloudWatchLogs client,
            ILogger<CloudWatchLoggerService>? logger,
            string logGroupName,
            string? logStreamName = null,
            bool useJsonFormat = true,
            string? origin = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? NullLogger<CloudWatchLoggerService>.Instance;
            _logGroupName = logGroupName ?? throw new ArgumentNullException(nameof(logGroupName));
            _logStreamName = !string.IsNullOrWhiteSpace(logStreamName)
                ? logStreamName
                : $"{Environment.MachineName}-{DateTime.UtcNow:yyyyMMdd}";
            UseJsonFormat = useJsonFormat;
            if (!string.IsNullOrWhiteSpace(origin))
                DefaultOrigin = origin;
        }

        /// <summary>
        /// Inicializa el grupo y stream de logs en CloudWatch.
        /// Si no se llama explícitamente, se ejecutará automáticamente en el primer log.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isInitialized) return;
                await EnsureInitializedInternalAsync().ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task EnsureInitializedInternalAsync()
        {
            try
            {
                var groups = await _client.DescribeLogGroupsAsync(
                    new DescribeLogGroupsRequest { LogGroupNamePrefix = _logGroupName }).ConfigureAwait(false);

                if (groups.LogGroups == null || !groups.LogGroups.Any(g => g.LogGroupName == _logGroupName))
                {
                    try
                    {
                        await _client.CreateLogGroupAsync(
                            new CreateLogGroupRequest { LogGroupName = _logGroupName }).ConfigureAwait(false);
                    }
                    catch (ResourceAlreadyExistsException)
                    {
                        // Grupo creado concurrentemente por otra instancia
                    }
                }

                var streams = await _client.DescribeLogStreamsAsync(
                    new DescribeLogStreamsRequest
                    {
                        LogGroupName = _logGroupName,
                        LogStreamNamePrefix = _logStreamName
                    }).ConfigureAwait(false);

                var existingStream = streams.LogStreams?.FirstOrDefault(s => s.LogStreamName == _logStreamName);
                if (existingStream == null)
                {
                    try
                    {
                        await _client.CreateLogStreamAsync(
                            new CreateLogStreamRequest { LogGroupName = _logGroupName, LogStreamName = _logStreamName })
                            .ConfigureAwait(false);
                        _sequenceToken = null;
                    }
                    catch (ResourceAlreadyExistsException)
                    {
                        // Stream creado concurrentemente por otra instancia
                    }
                }
                else
                {
                    _sequenceToken = existingStream.UploadSequenceToken;
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing CloudWatch log group '{LogGroup}' or stream '{LogStream}'.", _logGroupName, _logStreamName);
                throw;
            }
        }

        public override async Task LogAsync(LogEntry entry)
        {
            var formattedMessage = FormatEntry(entry);
            await SendLogAsync(formattedMessage, entry.Timestamp).ConfigureAwait(false);
        }

        private async Task SendLogAsync(string message, DateTime timestamp)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_isInitialized)
                {
                    await EnsureInitializedInternalAsync().ConfigureAwait(false);
                }

                var logEvent = new InputLogEvent
                {
                    Timestamp = timestamp,
                    Message = message
                };

                var request = new PutLogEventsRequest
                {
                    LogGroupName = _logGroupName,
                    LogStreamName = _logStreamName,
                    LogEvents = new List<InputLogEvent> { logEvent },
                    SequenceToken = _sequenceToken
                };

                try
                {
                    var response = await _client.PutLogEventsAsync(request).ConfigureAwait(false);
                    _sequenceToken = response.NextSequenceToken;
                }
                catch (InvalidSequenceTokenException ex)
                {
                    // Recuperación automática si otro proceso modificó el token de secuencia
                    _sequenceToken = ex.ExpectedSequenceToken;
                    request.SequenceToken = _sequenceToken;
                    var retryResponse = await _client.PutLogEventsAsync(request).ConfigureAwait(false);
                    _sequenceToken = retryResponse.NextSequenceToken;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send log to CloudWatch: {Message}", message);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_ownsClient)
            {
                _client.Dispose();
            }
            _semaphore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
