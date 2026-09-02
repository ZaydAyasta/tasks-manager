using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Features;

public sealed record CreateUserRequest(string? FullName, string? Email, string? Role, string? Password);
public sealed record ChangeUserRoleRequest(string? Role);

public sealed record UserDetailResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UserListItemResponse(Guid Id, string FullName, string Email, string Role, bool IsActive);

internal static class UserMappings
{
    public static UserDetailResponse ToDetailResponse(this User user) => new(
        user.Id,
        user.FullName,
        user.Email,
        user.Role.ToString(),
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt);

    public static UserListItemResponse ToListItemResponse(this User user) => new(
        user.Id,
        user.FullName,
        user.Email,
        user.Role.ToString(),
        user.IsActive);
}
