using DocManagement.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocManagement.Infrastructure.Messaging;

internal sealed class PreviewWorker(
    ChannelPreviewQueue queue,
    PendingPreviewRequeuer requeuer,
    IServiceScopeFactory scopeFactory,
    ILogger<PreviewWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await requeuer.RequeueAsync(stoppingToken);

        await foreach (var documentId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var previewService = scope.ServiceProvider.GetRequiredService<PreviewService>();
                await previewService.GenerateAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while processing preview for document {DocumentId}", documentId);
            }
        }
    }
}
