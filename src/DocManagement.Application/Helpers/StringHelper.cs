namespace DocManagement.Application.Helpers;

public static class StringHelper
{
    public static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
