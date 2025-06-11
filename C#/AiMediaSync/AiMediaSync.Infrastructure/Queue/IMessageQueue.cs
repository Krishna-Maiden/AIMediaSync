// AiMediaSync.Infrastructure/Queue/IMessageQueue.cs
namespace AiMediaSync.Infrastructure.Queue;

public interface IMessageQueue
{
    Task SendMessageAsync<T>(T message, string queueName, CancellationToken cancellationToken = default) where T : class;
    Task SendMessageAsync<T>(T message, string queueName, TimeSpan delay, CancellationToken cancellationToken = default) where T : class;
    Task<IEnumerable<QueueMessage<T>>> ReceiveMessagesAsync<T>(string queueName, int maxMessages = 1, CancellationToken cancellationToken = default) where T : class;
    Task CompleteMessageAsync(string queueName, string messageId, CancellationToken cancellationToken = default);
    Task AbandonMessageAsync(string queueName, string messageId, CancellationToken cancellationToken = default);
    Task CreateQueueIfNotExistsAsync(string queueName, CancellationToken cancellationToken = default);
    Task<long> GetQueueLengthAsync(string queueName, CancellationToken cancellationToken = default);
}

public class QueueMessage<T> where T : class
{
    public string MessageId { get; set; } = string.Empty;
    public T Body { get; set; } = null!;
    public DateTime EnqueuedTime { get; set; }
    public int DeliveryCount { get; set; }
    public string? LockToken { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}