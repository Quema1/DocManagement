using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocManagement.Application.Dtos;

namespace DocManagement.IntegrationTests.Fixtures;

internal static class HttpClientExtensions
{
    public static async Task<DocumentResponse> CreateDocumentAsync(this HttpClient client, CancellationToken cancellationToken, string name = "Report", string? description = "Q3 numbers")
    {
        var response = await client.PostAsJsonAsync("/api/documents", new DocumentRequest { Name = name, Description = description }, ApiFactory.Json, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentResponse>(ApiFactory.Json, cancellationToken))!;
    }

    public static async Task<(HttpStatusCode Status, JsonElement Body)> GetHealthAsync(this HttpClient client, string path, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return (response.StatusCode, body);
    }
}
