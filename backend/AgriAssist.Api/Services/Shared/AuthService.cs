using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using AgriAssist.Api.Configuration;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace AgriAssist.Api.Services.Shared;

public sealed class AuthService(
    AppDbContext dbContext,
    IConfiguration configuration,
    ICurrentUserService currentUser,
    IRequestValidator<RegisterFarmerRequest> registerFarmerValidator,
    IRequestValidator<LoginRequest> loginValidator,
    IRequestValidator<ChangeTemporaryPasswordRequest> changeTemporaryPasswordValidator,
    IPasswordPolicyService passwordPolicy) : IAuthService
{
    public async Task<AuthResponse> RegisterFarmerAsync(
        RegisterFarmerRequest request,
        CancellationToken cancellationToken)
    {
        Validate(registerFarmerValidator.Validate(request));
        await ValidatePasswordAsync(request.Password, request.Email, request.FullName, cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw EmailAlreadyExists();
        }

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Role = ApplicationRole.Farmer,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            MustChangePassword = false,
            PasswordChangedAt = now,
            TokenVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = null
        };

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw EmailAlreadyExists();
        }

        return CreateAccessAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        Validate(loginValidator.Validate(request));

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);

        if (user is null
            || user.IsDeleted
            || Encoding.UTF8.GetByteCount(request.Password) > PasswordPolicyService.BcryptMaximumInputBytes
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new ApiException(HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new ApiException(HttpStatusCode.Forbidden, "USER_INACTIVE", "This user account is inactive.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return user.MustChangePassword
            ? CreatePasswordChangeAuthResponse(user)
            : CreateAccessAuthResponse(user);
    }

    public async Task<AuthResponse> ChangeTemporaryPasswordAsync(
        ChangeTemporaryPasswordRequest request,
        CancellationToken cancellationToken)
    {
        Validate(changeTemporaryPasswordValidator.Validate(request));

        var userId = currentUser.UserId
            ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.Unauthorized, "TOKEN_INVALID_OR_EXPIRED", "The password-change token is invalid.");

        if (user.IsDeleted || !user.IsActive)
        {
            throw new ApiException(HttpStatusCode.Unauthorized, "TOKEN_INVALID_OR_EXPIRED", "The password-change token is invalid.");
        }

        if (!user.MustChangePassword)
        {
            throw new ApiException(HttpStatusCode.Conflict, "PASSWORD_CHANGE_NOT_REQUIRED", "A temporary password change is not required.");
        }

        await ValidatePasswordAsync(request.NewPassword, user.Email, user.FullName, cancellationToken);

        var now = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.PasswordChangedAt = now;
        user.TokenVersion = checked(user.TokenVersion + 1);
        user.UpdatedAt = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(
                HttpStatusCode.Unauthorized,
                "TOKEN_INVALID_OR_EXPIRED",
                "The password-change token is no longer valid.");
        }

        return CreateAccessAuthResponse(user);
    }

    public async Task<UserProfileResponse> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");
        var user = await dbContext.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "USER_NOT_FOUND", "User was not found.");

        return MapProfile(user);
    }

    private AuthResponse CreateAccessAuthResponse(AppUser user)
    {
        var (token, expiresAt) = CreateToken(user, AuthenticationTokenUses.Access, "Jwt:ExpiryMinutes", 60);
        return new AuthResponse(
            "authenticated",
            token,
            expiresAt,
            null,
            null,
            MapProfile(user));
    }

    private AuthResponse CreatePasswordChangeAuthResponse(AppUser user)
    {
        var (token, expiresAt) = CreateToken(
            user,
            AuthenticationTokenUses.PasswordChange,
            "Jwt:PasswordChangeExpiryMinutes",
            10);
        return new AuthResponse(
            "passwordChangeRequired",
            null,
            null,
            token,
            expiresAt,
            MapProfile(user));
    }

    private (string Token, DateTime ExpiresAt) CreateToken(
        AppUser user,
        string tokenUse,
        string expiryConfigurationKey,
        int defaultExpiryMinutes)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new ApiException(HttpStatusCode.InternalServerError, "JWT_NOT_CONFIGURED", "JWT signing is not configured.");
        }

        var expiryMinutes = int.TryParse(configuration[expiryConfigurationKey], out var minutes)
            ? minutes
            : defaultExpiryMinutes;
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(AuthenticationClaimNames.TokenVersion, user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(AuthenticationClaimNames.TokenUse, tokenUse)
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static UserProfileResponse MapProfile(AppUser user) =>
        new(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.MustChangePassword);

    private async ValueTask ValidatePasswordAsync(
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

    private static ApiException EmailAlreadyExists() =>
        new(HttpStatusCode.Conflict, "EMAIL_ALREADY_EXISTS", "A user with this email already exists.");

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };

    private static void Validate(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_FAILED", string.Join(" ", errors));
        }
    }
}
