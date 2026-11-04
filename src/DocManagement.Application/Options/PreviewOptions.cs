namespace DocManagement.Application.Options;

public class PreviewOptions
{
    public const string SectionName = "Preview";

    public string BaseUrl { get; set; } = "https://previews.docmanagement.local";

    public TimeSpan SimulatedDelay { get; set; } = TimeSpan.FromSeconds(5);
}
