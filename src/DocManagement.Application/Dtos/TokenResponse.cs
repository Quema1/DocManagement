namespace DocManagement.Application.Dtos;

public class TokenResponse
{
    public required string AccessToken { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
