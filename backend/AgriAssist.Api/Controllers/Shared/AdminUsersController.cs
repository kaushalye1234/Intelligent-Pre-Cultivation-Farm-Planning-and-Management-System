using AgriAssist.Api.Configuration;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgriAssist.Api.Controllers.Shared;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(ApplicationRole.Admin))]
[EnableRateLimiting(RateLimitPolicyNames.AdminUserManagement)]
public sealed class AdminUsersController(IUserService userService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> CreateStaff(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await userService.CreateStaffAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid userId,
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await userService.ResetStaffPasswordAsync(userId, request, cancellationToken);
        return NoContent();
    }
}
