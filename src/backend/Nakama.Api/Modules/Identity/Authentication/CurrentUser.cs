using System.Security.Claims;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Authentication;

public interface ICurrentUser
{
    Guid UserId { get; }
    string Email { get; }
    UserRole Role { get; }
    string CsrfToken { get; }
    bool IsAuthenticated { get; }
}

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public UserRole Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : default;
    public string CsrfToken => Principal?.FindFirstValue(BuildingBlocks.Security.CsrfProtection.ClaimType) ?? string.Empty;
}
