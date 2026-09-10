using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Dtos.Shared;

public sealed record UserListItemResponse(Guid Id, string FullName, string Email, ApplicationRole Role, bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record UpdateUserRoleRequest(ApplicationRole Role);

public sealed record SetUserActiveRequest(bool IsActive);
