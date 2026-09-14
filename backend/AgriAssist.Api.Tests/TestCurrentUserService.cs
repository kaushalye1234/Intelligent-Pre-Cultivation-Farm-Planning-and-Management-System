using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;

namespace AgriAssist.Api.Tests;

internal sealed class TestCurrentUserService(ApplicationRole role = ApplicationRole.ResourceOfficer) : ICurrentUserService
{
    public Guid? UserId { get; } = Guid.NewGuid();
    public ApplicationRole? Role { get; } = role;
    public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
}
