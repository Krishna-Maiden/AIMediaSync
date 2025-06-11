using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AiMediaSync.Infrastructure.Queue;

public class ServiceBusQueueOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DefaultQueueName { get; set; } = "processing-queue";
    public int MaxDeliveryCount { get; set; } = 3;
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan MessageTimeToLive { get; set; } = TimeSpan.FromDays(1);
}

public class ServiceBusQueue : IMessageQueue, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusQueue> _logger;
    private readonly ServiceBusQueueOptions _options;
    private readonly Dictionary<string, ServiceBusSender> _senders = new();
    private readonly Dictionary<string, ServiceBusReceiver> _receivers = new();

    public ServiceBusQueue(
        IOptions<ServiceBusQueueOptions> options,
        ILogger<ServiceBusQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new ServiceBusClient(_options.ConnectionString);
    }

    public async Task SendMessageAsync<T>(T message, string queueName, CancellationToken cancellationToken = default) where T : class
    {
        await SendMessageAsync(message, queueName, TimeSpan.Zero, cancellationToken);
    }

    public async Task SendMessageAsync<T>(T message, string queueName, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var sender = await GetSenderAsync(queueName);
            var messageBody = JsonSerializer.Serialize(message);
            
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                Subject = typeof(T).Name
            };

            if (delay > TimeSpan.Zero)
            {
                serviceBusMessage.ScheduledEnqueueTime = DateTimeOffset.UtcNow.Add(delay);
            }

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
            
            _logger.LogInformation("Message sent to Service Bus queue {QueueName}: {MessageType}", 
                queueName, typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Service Bus queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<IEnumerable<QueueMessage<T>>> ReceiveMessagesAsync<T>(string queueName, int maxMessages = 1, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var receiver = await GetReceiverAsync(queueName);
            var messages = await receiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), cancellationToken);
            
            var queueMessages = new List<QueueMessage<T>>();
            
            foreach (var message in messages)
            {
                try
                {
                    var body = JsonSerializer.Deserialize<T>(message.Body.ToString());
                    
                    queueMessages.Add(new QueueMessage<T>
                    {
                        MessageId = message.MessageId,
                        Body = body!,
                        EnqueuedTime = message.EnqueuedTime.DateTime,
                        DeliveryCount = message.DeliveryCount,
                        LockToken = message.LockToken,
                        Properties = message.ApplicationProperties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new()
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing message {MessageId} from queue {QueueName}", 
                        message.MessageId, queueName);
                    
                    // Dead letter the message if it can't be deserialized
                    await receiver.DeadLetterMessageAsync(message, "DeserializationError", ex.Message, cancellationToken);
                }
            }
            
            _logger.LogInformation("Received {Count} messages from Service Bus queue {QueueName}", 
                queueMessages.Count, queueName);
            
            return queueMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from Service Bus queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task CompleteMessageAsync(string queueName, string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var receiver = await GetReceiverAsync(queueName);
            await receiver.CompleteMessageAsync(await GetMessageByIdAsync(receiver, messageId), cancellationToken);
            
            _logger.LogInformation("Message {MessageId} completed in queue {QueueName}", messageId, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing message {MessageId} in queue {QueueName}", messageId, queueName);
            throw;
        }
    }

    public async Task AbandonMessageAsync(string queueName, string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var receiver = await GetReceiverAsync(queueName);
            await receiver.AbandonMessageAsync(await GetMessageByIdAsync(receiver, messageId), cancellationToken: cancellationToken);
            
            _logger.LogInformation("Message {MessageId} abandoned in queue {QueueName}", messageId, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error abandoning message {MessageId} in queue {QueueName}", messageId, queueName);
            throw;
        }
    }

    public async Task CreateQueueIfNotExistsAsync(string queueName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: This requires Service Bus management operations
            // In practice, queues should be created through Azure portal or ARM templates
            _logger.LogInformation("Queue creation requested for {QueueName} - ensure queue exists in Azure", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<long> GetQueueLengthAsync(string queueName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: This requires Service Bus management operations
            // Return 0 as placeholder - implement with ServiceBusAdministrationClient if needed
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue length for {QueueName}", queueName);
            throw;
        }
    }

    private async Task<ServiceBusSender> GetSenderAsync(string queueName)
    {
        if (!_senders.TryGetValue(queueName, out var sender))
        {
            sender = _client.CreateSender(queueName);
            _senders[queueName] = sender;
        }
        return sender;
    }

    private async Task<ServiceBusReceiver> GetReceiverAsync(string queueName)
    {
        if (!_receivers.TryGetValue(queueName, out var receiver))
        {
            receiver = _client.CreateReceiver(queueName);
            _receivers[queueName] = receiver;
        }
        return receiver;
    }

    private async Task<ServiceBusReceivedMessage> GetMessageByIdAsync(ServiceBusReceiver receiver, string messageId)
    {
        // This is a simplified implementation
        // In practice, you'd need to track messages or use session-based receivers
        throw new NotImplementedException("Message tracking by ID requires additional implementation");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
        
        foreach (var receiver in _receivers.Values)
        {
            await receiver.DisposeAsync();
        }
        
        await _client.DisposeAsync();
    }
}