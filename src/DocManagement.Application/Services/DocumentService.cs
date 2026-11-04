using DocManagement.Application.Dtos;
using DocManagement.Application.Helpers;
using DocManagement.Application.Interfaces;
using DocManagement.Domain;
using DocManagement.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace DocManagement.Application.Services;

public class DocumentService(IDocumentRepository repository, TimeProvider timeProvider, ILogger<DocumentService> logger)
{
    public async Task<PagedResult<DocumentResponse>> GetListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, PaginationConstants.DefaultPage);
        pageSize = Math.Clamp(pageSize, 1, PaginationConstants.MaxPageSize);

        var documents = await repository.GetListAsync((page - 1) * pageSize, pageSize, cancellationToken);
        var totalCount = await repository.CountAsync(cancellationToken);

        return new PagedResult<DocumentResponse>
        {
            Items = documents.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<DocumentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        return document is null ? null : ToResponse(document);
    }

    public async Task<DocumentResponse> CreateAsync(DocumentRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = StringHelper.TrimToNull(request.Name),
            Description = StringHelper.TrimToNull(request.Description),
            CreatedAt = now,
            UpdatedAt = now,
            PreviewStatus = PreviewStatus.None,
            PreviewUrl = null,
        };

        await repository.AddAsync(document, cancellationToken);
        logger.LogInformation("Document {DocumentId} created", document.Id);

        return ToResponse(document);
    }

    public async Task<DocumentResponse?> UpdateAsync(Guid id, DocumentRequest request, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var name = StringHelper.TrimToNull(request.Name);
        var description = StringHelper.TrimToNull(request.Description);

        if (document.Name == name && document.Description == description)
        {
            return ToResponse(document);
        }

        document.Name = name;
        document.Description = description;
        document.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        document.PreviewStatus = PreviewStatus.None;
        document.PreviewUrl = null;

        await repository.UpdateAsync(document, cancellationToken);
        logger.LogInformation("Document {DocumentId} updated", document.Id);

        return ToResponse(document);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return false;
        }

        await repository.DeleteAsync(document, cancellationToken);
        logger.LogInformation("Document {DocumentId} deleted", document.Id);

        return true;
    }

    private static DocumentResponse ToResponse(Document document) => new()
    {
        Id = document.Id,
        Name = document.Name ?? string.Empty,
        Description = document.Description,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt,
        PreviewStatus = document.PreviewStatus,
        PreviewUrl = document.PreviewUrl,
    };
}
