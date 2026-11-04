using DocManagement.Domain;

namespace DocManagement.Application.Interfaces;

public interface IDocumentRepository
{
    Task<IReadOnlyList<Document>> GetListAsync(int skip, int take, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Document document, CancellationToken cancellationToken = default);

    Task UpdateAsync(Document document, CancellationToken cancellationToken = default);

    Task UpdateAsync(Document document, OutboxMessage outboxMessage, CancellationToken cancellationToken = default);

    Task DeleteAsync(Document document, CancellationToken cancellationToken = default);
}
