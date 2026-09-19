using System.Data;
using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriAssist.Api.Services.Shared;

public sealed class AdminBootstrapService(
    AppDbContext dbContext,
    IPasswordPolicyService passwordPolicy) : IAdminBootstrapService
{
    private const long BootstrapAdvisoryLockKey = 4_164_752_019;

    public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken) =>
        dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Role == ApplicationRole.Admin, cancellationToken);

    public async Task<AppUser> BootstrapAsync(
        string fullName,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            if (string.Equals(
                    dbContext.Database.ProviderName,
                    "Npgsql.EntityFrameworkCore.PostgreSQL",
                    StringComparison.Ordinal))
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_xact_lock({BootstrapAdvisoryLockKey})",
                    cancellationToken);
            }

            var user = await BootstrapCoreAsync(fullName, email, password, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return user;
        }

        return await BootstrapCoreAsync(fullName, email, password, cancellationToken);
    }

    private async Task<AppUser> BootstrapCoreAsync(
        string fullName,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        if (await AnyAdminExistsAsync(cancellationToken))
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                "ADMIN_ALREADY_EXISTS",
                "Admin bootstrap is disabled because an Admin account already exists.");
        }

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 120)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                "VALIDATION_FAILED",
                "Full name is required and must be 120 characters or fewer.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length > 180 || !normalizedEmail.Contains('@'))
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                "VALIDATION_FAILED",
                "A valid email address is required.");
        }

        var passwordViolation = await passwordPolicy.ValidateAsync(
            password,
            normalizedEmail,
            fullName,
            cancellationToken);
        if (passwordViolation is not null)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                passwordViolation.Code,
                passwordViolation.Message);
        }

        if (await dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            throw new ApiException(
                HttpStatusCode.Conflict,
                "EMAIL_ALREADY_EXISTS",
                "A user with this email already exists.");
        }

        var now = DateTime.UtcNow;
        var admin = new AppUser
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = ApplicationRole.Admin,
            IsActive = true,
            MustChangePassword = false,
            PasswordChangedAt = now,
            TokenVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Users.Add(admin);
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
            throw new ApiException(
                HttpStatusCode.Conflict,
                "EMAIL_ALREADY_EXISTS",
                "A user with this email already exists.");
        }

        return admin;
    }
}
