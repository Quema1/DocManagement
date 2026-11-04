using System.Net;
using System.Net.Http.Json;
using DocManagement.Application.Dtos;
using DocManagement.Domain;
using DocManagement.Domain.Constants;
using DocManagement.IntegrationTests.Fixtures;

namespace DocManagement.IntegrationTests.Api;

public class DocumentsApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Crud_RoundTripsThroughDatabase()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);

        var created = await client.CreateDocumentAsync(Ct, name: "  Report  ", description: "Q3");
        Assert.Equal("Report", created.Name);
        Assert.Equal(PreviewStatus.None, created.PreviewStatus);

        var fetched = await client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{created.Id}", ApiFactory.Json, Ct);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("Q3", fetched.Description);
        Assert.Equal(DateTimeKind.Utc, fetched.CreatedAt.Kind);
        Assert.Equal(created.CreatedAt, fetched.CreatedAt);

        var updateResponse = await client.PutAsJsonAsync($"/api/documents/{created.Id}", new DocumentRequest { Name = "Renamed", Description = "Q4" }, ApiFactory.Json, Ct);
        var updated = await updateResponse.Content.ReadFromJsonAsync<DocumentResponse>(ApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal("Q4", updated.Description);

        var deleteResponse = await client.DeleteAsync($"/api/documents/{created.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await client.GetAsync($"/api/documents/{created.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsCreatedDocument()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);
        var created = await client.CreateDocumentAsync(Ct);

        var page = await client.GetFromJsonAsync<PagedResult<DocumentResponse>>($"/api/documents?pageSize={PaginationConstants.MaxPageSize}", ApiFactory.Json, Ct);

        Assert.Contains(page!.Items, d => d.Id == created.Id);
        Assert.True(page.TotalCount >= 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Create_ReturnsBadRequest_WhenNameIsMissing(string? name)
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);

        var response = await client.PostAsJsonAsync("/api/documents", new { Name = name }, ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameIsTooLong()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);

        var response = await client.PostAsJsonAsync("/api/documents", new { Name = new string('a', DocumentConstants.NameMaxLength + 1) }, ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenDocumentDoesNotExist()
    {
        var client = await factory.CreateAuthenticatedClientAsync(Ct);

        var response = await client.PutAsJsonAsync($"/api/documents/{Guid.NewGuid()}", new DocumentRequest { Name = "Report" }, ApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
