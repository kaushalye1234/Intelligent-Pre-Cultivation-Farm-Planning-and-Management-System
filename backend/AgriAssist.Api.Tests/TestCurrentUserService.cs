using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;

namespace AgriAssist.Api.Tests;

internal sealed class TestCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; } = Guid.NewGuid();
    public ApplicationRole? Role { get; } = ApplicationRole.ResourceOfficer;
    public bool IsInRole(ApplicationRole role) => Role == role;
}
