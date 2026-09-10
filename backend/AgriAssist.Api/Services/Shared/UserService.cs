using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Shared;

public sealed class UserService(AppDbContext dbContext) : IUserService
{
    public async Task<PagedResult<UserListItemResponse>> SearchAsync(PagedQuery query, ApplicationRole? role, bool? isActive, CancellationToken cancellationToken)
    {
        query.Normalize();
        var users = dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            users = users.Where(user => user.FullName.ToLower().Contains(search) || user.Email.ToLower().Contains(search));
        }

        if (role.HasValue)
        {
            users = users.Where(user => user.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            users = users.Where(user => user.IsActive == isActive.Value);
        }

        users = query.SortBy?.ToLowerInvariant() switch
        {
            "email" => query.SortDirection == "desc" ? users.OrderByDescending(user => user.Email) : users.OrderBy(user => user.Email),
            "role" => query.SortDirection == "desc" ? users.OrderByDescending(user => user.Role) : users.OrderBy(user => user.Role),
            "createdat" => query.SortDirection == "desc" ? users.OrderByDescending(user => user.CreatedAt) : users.OrderBy(user => user.CreatedAt),
            _ => query.SortDirection == "desc" ? users.OrderByDescending(user => user.FullName) : users.OrderBy(user => user.FullName)
        };

        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(user => new UserListItemResponse(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.CreatedAt, user.LastLoginAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItemResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<UserProfileResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        return new UserProfileResponse(user.Id, user.FullName, user.Email, user.Role, user.IsActive);
    }

    public async Task<UserProfileResponse> SetActiveAsync(Guid id, SetUserActiveRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new UserProfileResponse(user.Id, user.FullName, user.Email, user.Role, user.IsActive);
    }

    public async Task<UserProfileResponse> UpdateRoleAsync(Guid id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(ApplicationRole), request.Role))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "INVALID_ROLE", "Role is invalid.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new UserProfileResponse(user.Id, user.FullName, user.Email, user.Role, user.IsActive);
    }
}
