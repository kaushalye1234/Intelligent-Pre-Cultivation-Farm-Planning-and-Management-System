using AgriAssist.Api.Configuration;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgriAssist.Api.Controllers.Shared;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register-farmer")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.FarmerRegistration)]
    public async Task<ActionResult<AuthResponse>> RegisterFarmer(
        RegisterFarmerRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterFarmerAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Profile), response.User, response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Login)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return Ok(await authService.LoginAsync(request, cancellationToken));
    }

    [HttpPost("change-temporary-password")]
    [Authorize(Policy = AuthorizationPolicyNames.PasswordChange)]
    [EnableRateLimiting(RateLimitPolicyNames.TemporaryPasswordChange)]
    public async Task<ActionResult<AuthResponse>> ChangeTemporaryPassword(
        ChangeTemporaryPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.ChangeTemporaryPasswordAsync(request, cancellationToken));
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileResponse>> Profile(CancellationToken cancellationToken)
    {
        return Ok(await authService.GetCurrentProfileAsync(cancellationToken));
    }
}
