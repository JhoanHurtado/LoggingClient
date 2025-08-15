using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Microsoft.Extensions.Logging;
using LoggingClient.Interfaces;

namespace LoggingClient.CloudWatch
{
    public class CloudWatchLoggerService : ILoggerService
    {
        private readonly IAmazonCloudWatchLogs _client;
        private readonly ILogger<CloudWatchLoggerService> _logger;
        private readonly string _logGroupName;
        private readonly string _logStreamName;
        private string? _sequenceToken; // Nullable

        public CloudWatchLoggerService(
            IAmazonCloudWatchLogs client,
            ILogger<CloudWatchLoggerService> logger,
            string logGroupName,
            string logStreamName)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logGroupName = logGroupName ?? throw new ArgumentNullException(nameof(logGroupName));
            _logStreamName = logStreamName ?? throw new ArgumentNullException(nameof(logStreamName));
        }

        public async Task InitializeAsync()
        {
            try
            {
                var groups = await _client.DescribeLogGroupsAsync(
                    new DescribeLogGroupsRequest { LogGroupNamePrefix = _logGroupName }).ConfigureAwait(false);

                if (!groups.LogGroups.Any(g => g.LogGroupName == _logGroupName))
                {
                    await _client.CreateLogGroupAsync(
                        new CreateLogGroupRequest { LogGroupName = _logGroupName }).ConfigureAwait(false);
                }

                var streams = await _client.DescribeLogStreamsAsync(
                    new DescribeLogStreamsRequest
                    {
                        LogGroupName = _logGroupName,
                        LogStreamNamePrefix = _logStreamName
                    }).ConfigureAwait(false);

                var existingStream = streams.LogStreams.FirstOrDefault(s => s.LogStreamName == _logStreamName);
                if (existingStream == null)
                {
                    await _client.CreateLogStreamAsync(
                        new CreateLogStreamRequest { LogGroupName = _logGroupName, LogStreamName = _logStreamName })
                        .ConfigureAwait(false);
                    _sequenceToken = null;
                }
                else
                {
                    _sequenceToken = existingStream.UploadSequenceToken;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing CloudWatch log group or stream.");
                throw;
            }
        }

        public async Task LogInfoAsync(string message) => await SendLogAsync($"INFO: {message}").ConfigureAwait(false);

        public async Task LogErrorAsync(string message, Exception? ex = null)
            => await SendLogAsync($"ERROR: {message} {ex?.ToString()}").ConfigureAwait(false);

        private async Task SendLogAsync(string message)
        {
            try
            {
                var logEvent = new InputLogEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Message = message
                };

                var request = new PutLogEventsRequest
                {
                    LogGroupName = _logGroupName,
                    LogStreamName = _logStreamName,
                    LogEvents = new List<InputLogEvent> { logEvent },
                    SequenceToken = _sequenceToken
                };

                var response = await _client.PutLogEventsAsync(request).ConfigureAwait(false);
                _sequenceToken = response.NextSequenceToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send log to CloudWatch: {Message}", message);
            }
        }
    }
}