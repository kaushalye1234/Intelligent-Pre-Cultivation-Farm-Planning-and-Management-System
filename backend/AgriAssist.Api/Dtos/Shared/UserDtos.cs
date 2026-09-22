using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Dtos.Shared;

public sealed record UserListItemResponse(
    Guid Id,
    string FullName,
    string Email,
    ApplicationRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool MustChangePassword = false);

public sealed record UpdateUserRoleRequest(ApplicationRole Role);

public sealed record SetUserActiveRequest(bool IsActive);

public sealed record CreateStaffUserRequest(
    string FullName,
    string Email,
    ApplicationRole Role,
    string TemporaryPassword,
    string? CurrentAdminPassword);

public sealed record ResetStaffPasswordRequest(
    string TemporaryPassword,
    string? CurrentAdminPassword);

public sealed record AdminUserResponse(
    Guid Id,
    string FullName,
    string Email,
    ApplicationRole Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt);
