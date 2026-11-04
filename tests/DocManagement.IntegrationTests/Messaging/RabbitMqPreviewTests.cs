using System.Net;
using System.Net.Http.Json;
using DocManagement.Application.Dtos;
using DocManagement.Domain;
using DocManagement.IntegrationTests.Fixtures;

namespace DocManagement.IntegrationTests.Messaging;

public class RabbitMqPreviewTests(RabbitMqApiFactory factory) : IClassFixture<RabbitMqApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequestedPreview_BecomesReady_ThroughRabbitMq()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);
        var document = await client.CreateDocumentAsync(Ct);

        var response = await client.PostAsync($"/api/documents/{document.Id}/preview", null, Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var preview = await Poll.UntilAsync(
            () => client.GetFromJsonAsync<PreviewResponse>($"/api/documents/{document.Id}/preview", ApiFactory.Json, Ct),
            p => p?.Status == PreviewStatus.Ready,
            Ct);

        Assert.Equal(PreviewStatus.Ready, preview!.Status);
        Assert.Matches(TestConstants.PreviewUrlPattern, preview.Url);
    }
}
