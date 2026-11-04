using DocManagement.Domain;

namespace DocManagement.UnitTests.Fakes;

internal static class TestDocuments
{
    public static readonly DateTime CreatedAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public static Document Create(
        string name = "Report",
        string? description = "Q3 numbers",
        PreviewStatus previewStatus = PreviewStatus.None,
        string? previewUrl = null,
        DateTime? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = description,
        CreatedAt = createdAt ?? CreatedAt,
        UpdatedAt = createdAt ?? CreatedAt,
        PreviewStatus = previewStatus,
        PreviewUrl = previewUrl,
    };
}
