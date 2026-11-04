using System.Net;
using DocManagement.IntegrationTests.Fixtures;

namespace DocManagement.IntegrationTests.Api;

public class AuthTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Documents_ReturnUnauthorized_WithoutToken()
    {
        var response = await factory.CreateClient().GetAsync("/api/documents", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsAvailable_WithoutToken()
    {
        var response = await factory.CreateClient().GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
