using DocManagement.Domain.Constants;
using RabbitMQ.Client;

namespace DocManagement.Infrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqConnection(string connectionString) : IAsyncDisposable
{
    private readonly ConnectionFactory _factory = new()
    {
        Uri = new Uri(connectionString),
        ClientProvidedName = MessagingConstants.ClientName,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IChannel> OpenChannelAsync(CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await channel.QueueDeclareAsync(
            queue: MessagingConstants.PreviewQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        return channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _connection ??= await _factory.CreateConnectionAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}
