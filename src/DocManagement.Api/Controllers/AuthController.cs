using System.Security.Claims;
using DocManagement.Api.Options;
using DocManagement.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DocManagement.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class AuthController(IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider, IHostEnvironment environment) : ControllerBase
{
    [HttpPost("token")]
    public ActionResult<TokenResponse> CreateToken(TokenRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var jwt = jwtOptions.Value;
        var expiresAt = timeProvider.GetUtcNow().Add(jwt.Lifetime);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, request.UserName)]),
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        });

        return Ok(new TokenResponse { AccessToken = token, ExpiresAt = expiresAt });
    }
}
