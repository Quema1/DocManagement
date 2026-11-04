using DocManagement.Domain;

namespace DocManagement.Application.Dtos;

public class DocumentResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public PreviewStatus PreviewStatus { get; init; }
    public string? PreviewUrl { get; init; }
}
