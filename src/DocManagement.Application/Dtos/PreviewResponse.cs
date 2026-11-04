using DocManagement.Domain;

namespace DocManagement.Application.Dtos;

public class PreviewResponse
{
    public Guid DocumentId { get; init; }
    public PreviewStatus Status { get; init; }
    public string? Url { get; init; }
}
