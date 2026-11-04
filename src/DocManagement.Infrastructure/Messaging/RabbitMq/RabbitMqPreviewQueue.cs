using System.Text;
using DocManagement.Application.Interfaces;
using DocManagement.Domain.Constants;
using RabbitMQ.Client;

namespace DocManagement.Infrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqPreviewQueue(RabbitMqConnection connection) : IPreviewQueue, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public async ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(documentId.ToString());
        var properties = new BasicProperties { Persistent = true };

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not { IsOpen: true })
            {
                _channel = await connection.OpenChannelAsync(cancellationToken);
            }

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: MessagingConstants.PreviewQueueName,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _lock.Dispose();
    }
}
