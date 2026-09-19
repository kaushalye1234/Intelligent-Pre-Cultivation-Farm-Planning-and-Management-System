using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriAssist.Api.Services.Shared;

public sealed class UserService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IRequestValidator<CreateStaffUserRequest> createStaffValidator,
    IRequestValidator<ResetStaffPasswordRequest> resetPasswordValidator,
    IPasswordPolicyService passwordPolicy) : IUserService
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
            .Select(user => new UserListItemResponse(
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt,
                user.LastLoginAt,
                user.MustChangePassword))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItemResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<UserProfileResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        return MapProfile(user);
    }

    public async Task<UserProfileResponse> SetActiveAsync(Guid id, SetUserActiveRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        if (user.IsActive == request.IsActive)
        {
            return MapProfile(user);
        }

        if (!request.IsActive && user.Role == ApplicationRole.Admin)
        {
            await EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
        }

        user.IsActive = request.IsActive;
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = DateTime.UtcNow;
        await SaveSecurityChangeAsync(cancellationToken);
        return MapProfile(user);
    }

    public async Task<UserProfileResponse> UpdateRoleAsync(Guid id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(ApplicationRole), request.Role))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "INVALID_ROLE", "Role is invalid.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        if (user.Role == request.Role)
        {
            return MapProfile(user);
        }

        if (user.Role == ApplicationRole.Admin && request.Role != ApplicationRole.Admin)
        {
            await EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
        }

        user.Role = request.Role;
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = DateTime.UtcNow;
        await SaveSecurityChangeAsync(cancellationToken);
        return MapProfile(user);
    }

    public async Task<AdminUserResponse> CreateStaffAsync(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        Validate(createStaffValidator.Validate(request));
        await ValidatePasswordAsync(
            request.TemporaryPassword,
            request.Email,
            request.FullName,
            cancellationToken);

        if (request.Role == ApplicationRole.Admin)
        {
            await RequireAdminReauthenticationAsync(request.CurrentAdminPassword, cancellationToken);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            throw EmailAlreadyExists();
        }

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword),
            Role = request.Role,
            IsActive = true,
            MustChangePassword = true,
            PasswordChangedAt = null,
            TokenVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = currentUser.UserId
        };
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            throw EmailAlreadyExists();
        }

        return new AdminUserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            user.IsActive,
            user.MustChangePassword,
            user.CreatedAt);
    }

    public async Task ResetStaffPasswordAsync(
        Guid userId,
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken)
    {
        Validate(resetPasswordValidator.Validate(request));
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        if (user.Role == ApplicationRole.Farmer)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                "INVALID_STAFF_ROLE",
                "Farmer passwords are not reset through the staff-management endpoint.");
        }

        if (user.Role == ApplicationRole.Admin)
        {
            if (currentUser.UserId == user.Id)
            {
                throw new ApiException(
                    HttpStatusCode.Conflict,
                    "SELF_PASSWORD_RESET_NOT_ALLOWED",
                    "Use the authenticated password-change flow for your own account.");
            }

            await RequireAdminReauthenticationAsync(request.CurrentAdminPassword, cancellationToken);
        }

        await ValidatePasswordAsync(
            request.TemporaryPassword,
            user.Email,
            user.FullName,
            cancellationToken);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword);
        user.MustChangePassword = true;
        user.PasswordChangedAt = null;
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = DateTime.UtcNow;
        await SaveSecurityChangeAsync(cancellationToken);
    }

    private async Task RequireAdminReauthenticationAsync(
        string? currentAdminPassword,
        CancellationToken cancellationToken)
    {
        var actingAdminId = currentUser.UserId;
        var actingAdmin = actingAdminId is null
            ? null
            : await dbContext.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == actingAdminId, cancellationToken);

        var reauthenticationFailed = actingAdmin is null
            || actingAdmin.IsDeleted
            || !actingAdmin.IsActive
            || actingAdmin.Role != ApplicationRole.Admin
            || string.IsNullOrEmpty(currentAdminPassword)
            || System.Text.Encoding.UTF8.GetByteCount(currentAdminPassword) > PasswordPolicyService.BcryptMaximumInputBytes
            || !BCrypt.Net.BCrypt.Verify(currentAdminPassword, actingAdmin.PasswordHash);

        if (reauthenticationFailed)
        {
            throw new ApiException(
                HttpStatusCode.Forbidden,
                "ADMIN_REAUTHENTICATION_FAILED",
                "Current Admin password reauthentication failed.");
        }
    }

    private async Task ValidatePasswordAsync(
        string password,
        string email,
        string fullName,
        CancellationToken cancellationToken)
    {
        var violation = await passwordPolicy.ValidateAsync(password, email, fullName, cancellationToken);
        if (violation is not null)
        {
            throw new ApiException(HttpStatusCode.BadRequest, violation.Code, violation.Message);
        }
    }

    private async Task EnsureAnotherActiveAdminAsync(Guid targetAdminId, CancellationToken cancellationToken)
    {
        var anotherAdminExists = await dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                user => user.Id != targetAdminId
                    && user.Role == ApplicationRole.Admin
                    && user.IsActive
                    && !user.IsDeleted,
                cancellationToken);
        if (!anotherAdminExists)
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                "LAST_ACTIVE_ADMIN_REQUIRED",
                "The final active Admin cannot be deactivated or assigned another role.");
        }
    }

    private async Task SaveSecurityChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                "USER_SECURITY_STATE_CHANGED",
                "The user security state changed during this operation.");
        }
    }

    private static UserProfileResponse MapProfile(AppUser user) =>
        new(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.MustChangePassword);

    private static ApiException EmailAlreadyExists() =>
        new(HttpStatusCode.Conflict, "EMAIL_ALREADY_EXISTS", "A user with this email already exists.");

    private static void Validate(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_FAILED", string.Join(" ", errors));
        }
    }
}
