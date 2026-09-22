using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Services.Shared;

public interface IUserService
{
    Task<PagedResult<UserListItemResponse>> SearchAsync(PagedQuery query, ApplicationRole? role, bool? isActive, CancellationToken cancellationToken);
    Task<UserProfileResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<UserProfileResponse> SetActiveAsync(Guid id, SetUserActiveRequest request, CancellationToken cancellationToken);
    Task<UserProfileResponse> UpdateRoleAsync(Guid id, UpdateUserRoleRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse> CreateStaffAsync(CreateStaffUserRequest request, CancellationToken cancellationToken);
    Task ResetStaffPasswordAsync(Guid userId, ResetStaffPasswordRequest request, CancellationToken cancellationToken);
}
