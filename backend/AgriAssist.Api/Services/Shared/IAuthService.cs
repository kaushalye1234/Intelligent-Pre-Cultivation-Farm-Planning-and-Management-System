using AgriAssist.Api.Dtos.Shared;

namespace AgriAssist.Api.Services.Shared;

public interface IAuthService
{
    Task<AuthResponse> RegisterFarmerAsync(RegisterFarmerRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> ChangeTemporaryPasswordAsync(ChangeTemporaryPasswordRequest request, CancellationToken cancellationToken);
    Task<UserProfileResponse> GetCurrentProfileAsync(CancellationToken cancellationToken);
}
