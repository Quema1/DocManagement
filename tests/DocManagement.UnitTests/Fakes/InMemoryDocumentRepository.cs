using DocManagement.Application.Exceptions;
using DocManagement.Application.Interfaces;
using DocManagement.Domain;

namespace DocManagement.UnitTests.Fakes;

internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly Dictionary<Guid, Document> _documents = new();

    public Queue<Exception> UpdateFailures { get; } = new();

    public int SuccessfulUpdates { get; private set; }

    public IReadOnlyCollection<Document> All => _documents.Values.Select(Copy).ToList();

    public List<OutboxMessage> OutboxMessages { get; } = [];

    public Document Seed(Document document)
    {
        _documents[document.Id] = Copy(document);
        return document;
    }

    public Document Stored(Guid id) => Copy(_documents[id]);

    public Task<IReadOnlyList<Document>> GetListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> page = _documents.Values
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Skip(skip)
            .Take(take)
            .Select(Copy)
            .ToList();
        return Task.FromResult(page);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_documents.Count);

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.TryGetValue(id, out var document) ? Copy(document) : null);

    public Task<IReadOnlyList<Guid>> GetIdsByPreviewStatusAsync(PreviewStatus status, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> ids = _documents.Values.Where(d => d.PreviewStatus == status).Select(d => d.Id).ToList();
        return Task.FromResult(ids);
    }

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents.Add(document.Id, Copy(document));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        if (UpdateFailures.TryDequeue(out var failure))
        {
            throw failure;
        }

        if (!_documents.ContainsKey(document.Id))
        {
            throw new ConcurrencyConflictException("The document was deleted.");
        }

        _documents[document.Id] = Copy(document);
        SuccessfulUpdates++;
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Document document, OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        await UpdateAsync(document, cancellationToken);
        OutboxMessages.Add(outboxMessage);
    }

    public Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents.Remove(document.Id);
        return Task.CompletedTask;
    }

    private static Document Copy(Document document) => new()
    {
        Id = document.Id,
        Name = document.Name,
        Description = document.Description,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt,
        PreviewStatus = document.PreviewStatus,
        PreviewUrl = document.PreviewUrl,
    };
}
