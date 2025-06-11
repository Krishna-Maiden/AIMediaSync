// AiMediaSync.Infrastructure/Queue/RabbitMqQueue.cs
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AiMediaSync.Infrastructure.Queue;

public class RabbitMqQueueOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "aimediasync";
    public bool DurableQueues { get; set; } = true;
    public bool AutoAck { get; set; } = false;
}

public class RabbitMqQueue : IMessageQueue, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqQueue> _logger;
    private readonly RabbitMqQueueOptions _options;
    private readonly Dictionary<string, BasicGetResult> _pendingMessages = new();

    public RabbitMqQueue(
        IOptions<RabbitMqQueueOptions> options,
        ILogger<RabbitMqQueue> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory()
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange
        _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Direct, durable: true);
    }

    public async Task SendMessageAsync<T>(T message, string queueName, CancellationToken cancellationToken = default) where T : class
    {
        await SendMessageAsync(message, queueName, TimeSpan.Zero, cancellationToken);
    }

    public async Task SendMessageAsync<T>(T message, string queueName, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            await CreateQueueIfNotExistsAsync(queueName, cancellationToken);
            
            var messageBody = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageBody);
            
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = Guid.NewGuid().ToString();
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.Type = typeof(T).Name;

            if (delay > TimeSpan.Zero)
            {
                // Use RabbitMQ delayed message plugin or implement with TTL + DLX
                properties.Headers = new Dictionary<string, object>
                {
                    ["x-delay"] = (int)delay.TotalMilliseconds
                };
            }

            _channel.BasicPublish(
                exchange: _options.ExchangeName,
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Message sent to RabbitMQ queue {QueueName}: {MessageType}", 
                queueName, typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to RabbitMQ queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<IEnumerable<QueueMessage<T>>> ReceiveMessagesAsync<T>(string queueName, int maxMessages = 1, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            await CreateQueueIfNotExistsAsync(queueName, cancellationToken);
            
            var messages = new List<QueueMessage<T>>();
            
            for (int i = 0; i < maxMessages; i++)
            {
                var result = _channel.BasicGet(queueName, autoAck: _options.AutoAck);
                if (result == null) break;

                try
                {
                    var messageBody = Encoding.UTF8.GetString(result.Body.ToArray());
                    var body = JsonSerializer.Deserialize<T>(messageBody);
                    
                    var queueMessage = new QueueMessage<T>
                    {
                        MessageId = result.BasicProperties.MessageId ?? Guid.NewGuid().ToString(),
                        Body = body!,
                        EnqueuedTime = DateTimeOffset.FromUnixTimeSeconds(result.BasicProperties.Timestamp.UnixTime).DateTime,
                        DeliveryCount = result.Redelivered ? 2 : 1, // Simplified delivery count
                        LockToken = result.DeliveryTag.ToString()
                    };

                    if (result.BasicProperties.Headers != null)
                    {
                        queueMessage.Properties = result.BasicProperties.Headers
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    }

                    messages.Add(queueMessage);
                    
                    if (!_options.AutoAck)
                    {
                        _pendingMessages[queueMessage.MessageId] = result;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing message from queue {QueueName}", queueName);
                    
                    // Reject the message
                    _channel.BasicReject(result.DeliveryTag, requeue: false);
                }
            }

            _logger.LogInformation("Received {Count} messages from RabbitMQ queue {QueueName}", 
                messages.Count, queueName);
            
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from RabbitMQ queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task CompleteMessageAsync(string queueName, string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_pendingMessages.TryGetValue(messageId, out var message))
            {
                _channel.BasicAck(message.DeliveryTag, multiple: false);
                _pendingMessages.Remove(messageId);
                
                _logger.LogInformation("Message {MessageId} acknowledged in queue {QueueName}", messageId, queueName);
            }
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
            if (_pendingMessages.TryGetValue(messageId, out var message))
            {
                _channel.BasicReject(message.DeliveryTag, requeue: true);
                _pendingMessages.Remove(messageId);
                
                _logger.LogInformation("Message {MessageId} rejected in queue {QueueName}", messageId, queueName);
            }
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
            _channel.QueueDeclare(
                queue: queueName,
                durable: _options.DurableQueues,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            
            _channel.QueueBind(queueName, _options.ExchangeName, queueName);
            
            _logger.LogDebug("Queue {QueueName} declared and bound", queueName);
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
            var result = _channel.QueueDeclarePassive(queueName);
            return result.MessageCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue length for {QueueName}", queueName);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}