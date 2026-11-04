using System.Net;
using DocManagement.Domain.Constants;
using DocManagement.IntegrationTests.Fixtures;

namespace DocManagement.IntegrationTests.Messaging;

public class RabbitMqHealthCheckTests(RabbitMqApiFactory factory) : IClassFixture<RabbitMqApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_ChecksDatabaseAndRabbitMq_ForRabbitMqProvider()
    {
        var (status, body) = await factory.CreateClient().GetHealthAsync(HealthCheckConstants.Path, Ct);

        Assert.Equal(HttpStatusCode.OK, status);
        var checks = body.GetProperty("checks");
        Assert.Equal("Healthy", checks.GetProperty(HealthCheckConstants.DatabaseCheckName).GetProperty("status").GetString());
        Assert.Equal("Healthy", checks.GetProperty(HealthCheckConstants.RabbitMqCheckName).GetProperty("status").GetString());
    }
}
