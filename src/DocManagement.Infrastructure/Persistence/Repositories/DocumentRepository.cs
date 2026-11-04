using DocManagement.Application.Exceptions;
using DocManagement.Application.Interfaces;
using DocManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace DocManagement.Infrastructure.Persistence.Repositories;

public class DocumentRepository(AppDbContext dbContext) : IDocumentRepository
{
    public async Task<IReadOnlyList<Document>> GetListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Documents.CountAsync(cancellationToken);
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Document document, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(document).State == EntityState.Detached)
        {
            dbContext.Documents.Update(document);
        }

        await SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Document document, OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        dbContext.OutboxMessages.Add(outboxMessage);
        await UpdateAsync(document, cancellationToken);
    }

    public async Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
    {
        dbContext.Documents.Remove(document);
        await SaveChangesAsync(cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The document was modified or deleted by another operation.", ex);
        }
    }
}
