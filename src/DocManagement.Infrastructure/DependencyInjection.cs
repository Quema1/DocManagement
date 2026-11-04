using DocManagement.Application.Interfaces;
using DocManagement.Domain.Constants;
using DocManagement.Infrastructure.Messaging.Outbox;
using DocManagement.Infrastructure.Messaging.RabbitMq;
using DocManagement.Infrastructure.Persistence;
using DocManagement.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseConnectionString = GetRequiredConnectionString(configuration, ConnectionStringNames.Database);

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(databaseConnectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IDocumentRepository, DocumentRepository>();

        var rabbitMqConnectionString = GetRequiredConnectionString(configuration, ConnectionStringNames.RabbitMq);

        services.AddSingleton(_ => new RabbitMqConnection(rabbitMqConnectionString));
        services.AddSingleton<IPreviewQueue, RabbitMqPreviewQueue>();
        services.AddHostedService<OutboxPublisher>();
        services.AddHostedService<RabbitMqPreviewConsumer>();

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
