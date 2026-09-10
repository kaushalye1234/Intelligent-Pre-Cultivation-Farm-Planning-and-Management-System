using System.Security.Claims;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Services.Shared;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    ApplicationRole? Role { get; }
    bool IsInRole(ApplicationRole role);
}

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public ApplicationRole? Role
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<ApplicationRole>(value, out var role) ? role : null;
        }
    }

    public bool IsInRole(ApplicationRole role) => Role == role;
}
