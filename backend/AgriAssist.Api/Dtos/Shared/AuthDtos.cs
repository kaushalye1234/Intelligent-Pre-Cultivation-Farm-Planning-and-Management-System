using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Dtos.Shared;

public sealed record RegisterRequest(string FullName, string Email, string Password, ApplicationRole Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserProfileResponse User);

public sealed record UserProfileResponse(Guid Id, string FullName, string Email, ApplicationRole Role, bool IsActive);
