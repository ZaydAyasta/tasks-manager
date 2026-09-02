namespace Nakama.Api.Modules.Identity.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";
    public string Issuer { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public string SigningKey { get; init; } = null!;
    public int AccessTokenMinutes { get; init; } = 60;
}

public static class Policies
{
    public const string AuthenticatedUser = "AuthenticatedUser";
    public const string Admin = "Admin";
}
