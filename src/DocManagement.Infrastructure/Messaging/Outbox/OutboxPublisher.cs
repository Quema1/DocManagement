using DocManagement.Application.Interfaces;
using DocManagement.Domain;
using DocManagement.Domain.Constants;
using DocManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Exceptions;

namespace DocManagement.Infrastructure.Messaging.Outbox;

internal sealed class OutboxPublisher(
    IPreviewQueue queue,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private DateTime _nextCleanupAt = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(OutboxConstants.PollIntervalSeconds), timeProvider);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await PublishPendingAsync(dbContext, stoppingToken);
                await CleanupAsync(dbContext, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox processing cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishPendingAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var processed = 0;

        while (processed < OutboxConstants.MaxMessagesPerCycle)
        {
            var now = UtcNow;
            var messages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
                .OrderBy(m => m.OccurredAt)
                .Take(OutboxConstants.BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                if (!await PublishAsync(dbContext, message, cancellationToken))
                {
                    return;
                }

                processed++;
            }

            if (messages.Count < OutboxConstants.BatchSize)
            {
                return;
            }
        }
    }

    private async Task<bool> PublishAsync(AppDbContext dbContext, OutboxMessage message, CancellationToken cancellationToken)
    {
        if (message.Type != OutboxConstants.PreviewRequestedType || !Guid.TryParse(message.Payload, out var documentId))
        {
            logger.LogError("Dead-lettering unsupported outbox message {OutboxMessageId} of type {Type}", message.Id, message.Type);
            message.Error = $"Unsupported message type '{message.Type}' or payload.";
            message.ProcessedAt = UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        try
        {
            await queue.EnqueueAsync(documentId, cancellationToken);
            message.ProcessedAt = UtcNow;
            message.Error = null;

            logger.LogInformation("Published outbox message {OutboxMessageId} for document {DocumentId} after {Attempts} failed attempt(s)",
                message.Id, documentId, message.Attempts);
        }
        catch (Exception ex) when (IsBrokerUnavailable(ex))
        {
            logger.LogWarning(ex, "Message broker is unavailable, outbox publishing paused until the next cycle");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleFailureAsync(dbContext, message, documentId, ex, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task HandleFailureAsync(
        AppDbContext dbContext,
        OutboxMessage message,
        Guid documentId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (OutboxRetryPolicy.RegisterFailure(message, exception.Message, UtcNow))
        {
            logger.LogError(exception, "Dead-lettering outbox message {OutboxMessageId} for document {DocumentId} after {Attempts} attempts",
                message.Id, documentId, message.Attempts);
            await MarkPreviewFailedAsync(dbContext, documentId, cancellationToken);
            return;
        }

        logger.LogWarning(exception, "Could not publish outbox message {OutboxMessageId} for document {DocumentId} (attempt {Attempts}), next attempt at {NextAttemptAt}",
            message.Id, documentId, message.Attempts, message.NextAttemptAt);
    }

    private static async Task MarkPreviewFailedAsync(AppDbContext dbContext, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.FindAsync([documentId], cancellationToken);
        if (document is { PreviewStatus: PreviewStatus.Pending })
        {
            document.PreviewStatus = PreviewStatus.Failed;
            document.PreviewUrl = null;
        }
    }

    private async Task CleanupAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = UtcNow;
        if (now < _nextCleanupAt)
        {
            return;
        }

        var cutoff = now.AddHours(-OutboxConstants.RetentionHours);
        var deleted = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        _nextCleanupAt = now.AddMinutes(OutboxConstants.CleanupIntervalMinutes);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} processed outbox message(s)", deleted);
        }
    }

    private static bool IsBrokerUnavailable(Exception exception) =>
        exception is BrokerUnreachableException or ConnectFailureException or OperationInterruptedException;

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
}
