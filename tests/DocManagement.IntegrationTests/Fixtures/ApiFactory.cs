using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocManagement.Application.Dtos;
using DocManagement.Application.Options;
using DocManagement.Domain.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(DocManagement.IntegrationTests.Fixtures.ApiFactory))]

namespace DocManagement.IntegrationTests.Fixtures;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly MsSqlContainer _database = new MsSqlBuilder(TestConstants.SqlServerImage).Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder(TestConstants.RabbitMqImage).Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _rabbitMq.StartAsync());
        StartServer();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = new SqlConnectionStringBuilder(_database.GetConnectionString())
        {
            InitialCatalog = TestConstants.DatabaseName,
        }.ConnectionString;

        builder.UseEnvironment(Environments.Development);
        builder.UseSetting($"ConnectionStrings:{ConnectionStringNames.Database}", connectionString);
        builder.UseSetting($"ConnectionStrings:{ConnectionStringNames.RabbitMq}", _rabbitMq.GetConnectionString());
        builder.UseSetting($"{PreviewOptions.SectionName}:{nameof(PreviewOptions.BaseUrl)}", TestConstants.PreviewBaseUrl);
        builder.UseSetting($"{PreviewOptions.SectionName}:{nameof(PreviewOptions.SimulatedDelay)}", TimeSpan.Zero.ToString());
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token", new { UserName = $"user-{Guid.NewGuid():N}" }, Json, cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
