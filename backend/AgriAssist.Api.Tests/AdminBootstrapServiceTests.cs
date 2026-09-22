using AgriAssist.Api.Configuration;
using AgriAssist.Api.Data;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.Tests;

public sealed class AdminBootstrapServiceTests
{
    [Fact]
    public async Task Creates_first_admin_with_permanent_password_state()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var admin = await service.BootstrapAsync(
            "First Admin",
            "FIRST.ADMIN@example.com",
            "secure bootstrap phrase",
            CancellationToken.None);

        Assert.Equal(ApplicationRole.Admin, admin.Role);
        Assert.Equal("first.admin@example.com", admin.Email);
        Assert.True(admin.IsActive);
        Assert.False(admin.MustChangePassword);
        Assert.NotNull(admin.PasswordChangedAt);
        Assert.Equal(1, admin.TokenVersion);
        Assert.True(BCrypt.Net.BCrypt.Verify("secure bootstrap phrase", admin.PasswordHash));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Refuses_when_any_admin_record_exists(bool isActive, bool isDeleted)
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new AppUser
        {
            FullName = "Existing Admin",
            Email = "existing.admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("existing admin phrase"),
            Role = ApplicationRole.Admin,
            IsActive = isActive,
            IsDeleted = isDeleted
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.BootstrapAsync(
                "Second Admin",
                "second.admin@example.com",
                "secure bootstrap phrase",
                CancellationToken.None));

        Assert.Equal("ADMIN_ALREADY_EXISTS", exception.Code);
        Assert.Single(dbContext.Users);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminBootstrap-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static AdminBootstrapService CreateService(AppDbContext dbContext)
    {
        var compromisedChecker = new ConfiguredCompromisedPasswordChecker(
            Options.Create(new PasswordSecurityOptions()));
        var passwordPolicy = new PasswordPolicyService(compromisedChecker);
        return new AdminBootstrapService(dbContext, passwordPolicy);
    }
}
