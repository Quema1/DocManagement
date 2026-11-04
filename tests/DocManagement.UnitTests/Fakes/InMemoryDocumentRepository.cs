using DocManagement.Application.Interfaces;
using DocManagement.Domain;

namespace DocManagement.UnitTests.Fakes;

internal sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly Dictionary<Guid, Document> _documents = new();

    public Queue<Exception> UpdateFailures { get; } = new();

    public int SuccessfulUpdates { get; private set; }

    public IReadOnlyCollection<Document> All => _documents.Values;

    public Document Seed(Document document)
    {
        _documents[document.Id] = document;
        return document;
    }

    public Task<IReadOnlyList<Document>> GetListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> page = _documents.Values
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Skip(skip)
            .Take(take)
            .ToList();
        return Task.FromResult(page);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_documents.Count);

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.GetValueOrDefault(id));

    public Task<IReadOnlyList<Guid>> GetIdsByPreviewStatusAsync(PreviewStatus status, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> ids = _documents.Values.Where(d => d.PreviewStatus == status).Select(d => d.Id).ToList();
        return Task.FromResult(ids);
    }

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents.Add(document.Id, document);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        if (UpdateFailures.TryDequeue(out var failure))
        {
            throw failure;
        }

        _documents[document.Id] = document;
        SuccessfulUpdates++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents.Remove(document.Id);
        return Task.CompletedTask;
    }
}
