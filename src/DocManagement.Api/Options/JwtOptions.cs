using System.ComponentModel.DataAnnotations;
using System.Text;
using DocManagement.Domain.Constants;
using Microsoft.IdentityModel.Tokens;

namespace DocManagement.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(AuthConstants.SigningKeyMinLength)]
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(1);

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
