using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Dtos.Shared;

public sealed record RegisterFarmerRequest(string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangeTemporaryPasswordRequest(string NewPassword);

public sealed record AuthResponse(
    string AuthenticationStatus,
    string? AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? PasswordChangeToken,
    DateTime? PasswordChangeTokenExpiresAt,
    UserProfileResponse User);

public sealed record UserProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    ApplicationRole Role,
    bool IsActive,
    bool MustChangePassword = false);
