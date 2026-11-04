using System.Net;
using System.Net.Http.Json;
using DocManagement.Application.Dtos;
using DocManagement.Domain;
using DocManagement.IntegrationTests.Fixtures;
using DocManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocManagement.IntegrationTests.Api;

public class PreviewsApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequestedPreview_BecomesReady_ThroughOutboxAndWorker()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);
        var document = await client.CreateDocumentAsync(Ct);

        var response = await client.PostAsync($"/api/documents/{document.Id}/preview", null, Ct);
        var accepted = await response.Content.ReadFromJsonAsync<PreviewResponse>(ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(PreviewStatus.Pending, accepted!.Status);

        var preview = await Poll.UntilAsync(
            () => client.GetFromJsonAsync<PreviewResponse>($"/api/documents/{document.Id}/preview", ApiFactory.Json, Ct),
            p => p?.Status == PreviewStatus.Ready,
            Ct);

        Assert.Equal(PreviewStatus.Ready, preview!.Status);
        Assert.Matches(TestConstants.PreviewUrlPattern, preview.Url);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await dbContext.OutboxMessages.SingleAsync(m => m.Payload == document.Id.ToString(), Ct);
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.Error);
    }

    [Fact]
    public async Task RequestingReadyPreview_ReturnsOkWithSameUrl()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);
        var document = await client.CreateDocumentAsync(Ct);
        await client.PostAsync($"/api/documents/{document.Id}/preview", null, Ct);

        var ready = await Poll.UntilAsync(
            () => client.GetFromJsonAsync<PreviewResponse>($"/api/documents/{document.Id}/preview", ApiFactory.Json, Ct),
            p => p?.Status == PreviewStatus.Ready,
            Ct);

        var response = await client.PostAsync($"/api/documents/{document.Id}/preview", null, Ct);
        var again = await response.Content.ReadFromJsonAsync<PreviewResponse>(ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ready!.Url, again!.Url);
    }

    [Fact]
    public async Task Request_ReturnsNotFound_WhenDocumentDoesNotExist()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);

        var response = await client.PostAsync($"/api/documents/{Guid.NewGuid()}/preview", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Request_IsRateLimitedPerUser()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);
        var document = await client.CreateDocumentAsync(Ct);
        var url = $"/api/documents/{document.Id}/preview";

        for (var i = 0; i < 5; i++)
        {
            var allowed = await client.PostAsync(url, null, Ct);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var rejected = await client.PostAsync(url, null, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);

        var otherUser = await factory.CreateAuthenticatedClientAsync(Ct);
        var otherUserResponse = await otherUser.PostAsync(url, null, Ct);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherUserResponse.StatusCode);
    }
}
