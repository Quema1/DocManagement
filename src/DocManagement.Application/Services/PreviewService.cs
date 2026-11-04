using DocManagement.Application.Dtos;
using DocManagement.Application.Exceptions;
using DocManagement.Application.Interfaces;
using DocManagement.Application.Options;
using DocManagement.Application.Helpers;
using DocManagement.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocManagement.Application.Services;

public class PreviewService(
    IDocumentRepository repository,
    IPreviewQueue queue,
    UrlHelper urlHelper,
    IOptions<PreviewOptions> options,
    TimeProvider timeProvider,
    ILogger<PreviewService> logger)
{
    public async Task<PreviewResponse?> CreateAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (document.PreviewStatus is PreviewStatus.None or PreviewStatus.Failed)
        {
            document.PreviewStatus = PreviewStatus.Pending;
            document.PreviewUrl = null;
            await repository.UpdateAsync(document, cancellationToken);

            try
            {
                await queue.EnqueueAsync(document.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not queue preview generation for document {DocumentId}", document.Id);
                await MarkFailedAsync(document, cancellationToken);
                throw new PreviewQueueUnavailableException($"Could not queue preview generation for document {document.Id}.", ex);
            }

            logger.LogInformation("Preview generation queued for document {DocumentId}", document.Id);
        }

        return ToResponse(document);
    }

    public async Task<PreviewResponse?> GetAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        return document is null ? null : ToResponse(document);
    }

    public async Task GenerateAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);

        if (document is not { PreviewStatus: PreviewStatus.Pending })
        {
            logger.LogInformation("Skipping preview generation for document {DocumentId}: no longer pending", documentId);
            return;
        }

        try
        {
            await Task.Delay(options.Value.SimulatedDelay, timeProvider, cancellationToken);

            document.PreviewUrl = urlHelper.Generate(document);
            document.PreviewStatus = PreviewStatus.Ready;
            await repository.UpdateAsync(document, cancellationToken);

            logger.LogInformation("Preview generated for document {DocumentId}", documentId);
        }
        catch (ConcurrencyConflictException)
        {
            logger.LogInformation("Discarding preview for document {DocumentId}: it changed during generation", documentId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Preview generation failed for document {DocumentId}", documentId);
            await MarkFailedAsync(document, cancellationToken);
        }
    }

    private async Task MarkFailedAsync(Document document, CancellationToken cancellationToken)
    {
        try
        {
            document.PreviewStatus = PreviewStatus.Failed;
            document.PreviewUrl = null;
            await repository.UpdateAsync(document, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not mark preview of document {DocumentId} as failed", document.Id);
        }
    }

    private static PreviewResponse ToResponse(Document document) => new()
    {
        DocumentId = document.Id,
        Status = document.PreviewStatus,
        Url = document.PreviewUrl,
    };
}
