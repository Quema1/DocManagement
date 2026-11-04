using DocManagement.Application.Interfaces;
using DocManagement.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocManagement.Infrastructure.Messaging.InMemory;

internal sealed class PendingPreviewRequeuer(
    IPreviewQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PendingPreviewRequeuer> logger)
{
    public async Task RequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var pendingIds = await repository.GetIdsByPreviewStatusAsync(PreviewStatus.Pending, cancellationToken);

            foreach (var id in pendingIds)
            {
                await queue.EnqueueAsync(id, cancellationToken);
            }

            if (pendingIds.Count > 0)
            {
                logger.LogInformation("Re-queued {Count} pending preview(s) after startup", pendingIds.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not re-queue pending previews on startup");
        }
    }
}
