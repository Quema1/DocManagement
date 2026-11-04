using DocManagement.Domain;
using DocManagement.IntegrationTests.Fixtures;
using DocManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocManagement.IntegrationTests.Messaging;

public class OutboxPublisherTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnsupportedMessage_IsDeadLettered()
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "Unknown",
            Payload = "not-a-guid",
            OccurredAt = DateTime.UtcNow,
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync(Ct);
        }

        var stored = await Poll.UntilAsync(
            async () =>
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return await dbContext.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id, Ct);
            },
            m => m.ProcessedAt is not null,
            Ct);

        Assert.NotNull(stored.ProcessedAt);
        Assert.Contains("Unsupported", stored.Error);
    }
}
