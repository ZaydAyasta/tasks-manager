using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Authentication;

public interface IJwtTokenService
{
    AuthToken Create(User user);
}

public sealed record AuthToken(string AccessToken, DateTimeOffset ExpiresAt);

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    public AuthToken Create(User user)
    {
        var value = options.Value;
        var expiresAt = clock.UtcNow.AddMinutes(value.AccessTokenMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(value.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(value.Issuer, value.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role.ToString())],
            expires: expiresAt.UtcDateTime, signingCredentials: credentials);
        return new AuthToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
