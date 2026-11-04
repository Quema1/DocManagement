using System.Text;
using DocManagement.Application.Services;
using DocManagement.Domain.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace DocManagement.Infrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqPreviewConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqPreviewConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await OpenChannelWithRetryAsync(stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: MessagingConstants.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, args) => HandleAsync(channel, args, stoppingToken);
        await channel.BasicConsumeAsync(MessagingConstants.PreviewQueueName, autoAck: false, consumer, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken stoppingToken)
    {
        if (!Guid.TryParse(Encoding.UTF8.GetString(args.Body.Span), out var documentId))
        {
            logger.LogWarning("Rejecting malformed preview message {DeliveryTag}", args.DeliveryTag);
            await channel.BasicRejectAsync(args.DeliveryTag, requeue: false, stoppingToken);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var previewService = scope.ServiceProvider.GetRequiredService<PreviewService>();
            await previewService.GenerateAsync(documentId, stoppingToken);

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error while processing preview for document {DocumentId}", documentId);

            if (args.Redelivered)
            {
                await MarkFailedAsync(documentId, stoppingToken);
            }

            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: !args.Redelivered, stoppingToken);
        }
    }

    private async Task MarkFailedAsync(Guid documentId, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var previewService = scope.ServiceProvider.GetRequiredService<PreviewService>();
        await previewService.MarkFailedAsync(documentId, stoppingToken);
    }

    private async Task<IChannel> OpenChannelWithRetryAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                return await connection.OpenChannelAsync(stoppingToken);
            }
            catch (BrokerUnreachableException ex)
            {
                logger.LogWarning(ex, "RabbitMQ is unreachable, retrying in {Seconds}s", MessagingConstants.ReconnectDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(MessagingConstants.ReconnectDelaySeconds), stoppingToken);
            }
        }
    }
}
