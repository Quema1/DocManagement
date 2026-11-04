using DocManagement.Domain.Constants;
using DocManagement.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.RabbitMq;

namespace DocManagement.IntegrationTests.Fixtures;

public sealed class RabbitMqApiFactory : ApiFactory
{
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder(TestConstants.RabbitMqImage).Build();

    public override async ValueTask InitializeAsync()
    {
        await _rabbitMq.StartAsync();
        await base.InitializeAsync();
    }

    protected override void ConfigureMessaging(IWebHostBuilder builder)
    {
        builder.UseSetting($"{MessagingOptions.SectionName}:{nameof(MessagingOptions.Provider)}", nameof(MessagingProvider.RabbitMq));
        builder.UseSetting($"ConnectionStrings:{ConnectionStringNames.RabbitMq}", _rabbitMq.GetConnectionString());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
