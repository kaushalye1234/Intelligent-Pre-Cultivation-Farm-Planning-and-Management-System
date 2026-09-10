using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriAssist.Api.Controllers.Shared;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(ApplicationRole.Admin))]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemResponse>>> Search(
        [FromQuery] PagedQuery query,
        [FromQuery] ApplicationRole? role,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        return Ok(await userService.SearchAsync(query, role, isActive, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserProfileResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await userService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<UserProfileResponse>> SetActive(Guid id, SetUserActiveRequest request, CancellationToken cancellationToken)
    {
        return Ok(await userService.SetActiveAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<ActionResult<UserProfileResponse>> UpdateRole(Guid id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await userService.UpdateRoleAsync(id, request, cancellationToken));
    }
}
