using DocManagement.Application.Exceptions;
using DocManagement.Application.Interfaces;
using DocManagement.Domain;
using DocManagement.Domain.Constants;
using DocManagement.Infrastructure.Persistence;
using DocManagement.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace DocManagement.IntegrationTests.Persistence;

public class DocumentRepositoryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UpdateAsync_ThrowsConcurrencyConflict_WhenDocumentChangedMeanwhile()
    {
        var id = await SeedAsync();

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var first = (await firstRepository.GetByIdAsync(id, Ct))!;
        var second = (await secondRepository.GetByIdAsync(id, Ct))!;

        first.Name = "First";
        await firstRepository.UpdateAsync(first, Ct);

        second.Name = "Second";
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => secondRepository.UpdateAsync(second, Ct));
    }

    [Fact]
    public async Task UpdateAsync_WithOutboxMessage_DoesNotStoreMessage_WhenUpdateConflicts()
    {
        var id = await SeedAsync();

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IDocumentRepository>();

        var first = (await firstRepository.GetByIdAsync(id, Ct))!;
        var second = (await secondRepository.GetByIdAsync(id, Ct))!;

        first.Name = "First";
        await firstRepository.UpdateAsync(first, Ct);

        second.PreviewStatus = PreviewStatus.Pending;
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = OutboxConstants.PreviewRequestedType,
            Payload = id.ToString(),
            OccurredAt = DateTime.UtcNow,
        };
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => secondRepository.UpdateAsync(second, message, Ct));

        await using var checkScope = factory.Services.CreateAsyncScope();
        var dbContext = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(await dbContext.OutboxMessages.FindAsync([message.Id], Ct));
    }

    private async Task<Guid> SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = "Report",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await repository.AddAsync(document, Ct);
        return document.Id;
    }
}
