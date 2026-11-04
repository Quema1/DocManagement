using DocManagement.Application.Interfaces;
using DocManagement.Domain.Constants;
using DocManagement.Infrastructure.Messaging;
using DocManagement.Infrastructure.Persistence;
using DocManagement.Infrastructure.Repositories;
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

        var messaging = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddSingleton<PendingPreviewRequeuer>();

        if (messaging.Provider == MessagingProvider.RabbitMq)
        {
            var rabbitMqConnectionString = GetRequiredConnectionString(configuration, ConnectionStringNames.RabbitMq);

            services.AddSingleton(_ => new RabbitMqConnection(rabbitMqConnectionString));
            services.AddSingleton<IPreviewQueue, RabbitMqPreviewQueue>();
            services.AddHostedService<RabbitMqPreviewConsumer>();
        }
        else
        {
            services.AddSingleton<ChannelPreviewQueue>();
            services.AddSingleton<IPreviewQueue>(sp => sp.GetRequiredService<ChannelPreviewQueue>());
            services.AddHostedService<PreviewWorker>();
        }

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");
}
