using System.Net.Http.Json;
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
}
