using System.Net.Http.Json;
using AgriAssist.Api.Bootstrap;
using AgriAssist.Api.Configuration;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.Tests;

public sealed class AdminBootstrapOnStartupServiceTests
{
    [Fact]
    public async Task Does_nothing_when_startup_bootstrap_is_disabled()
    {
        await using var dbContext = CreateDbContext();
        var startupService = CreateService(dbContext, new Dictionary<string, string?>());

        var result = await startupService.RunIfEnabledAsync(CancellationToken.None);

        Assert.Equal(AdminBootstrapStartupResult.Disabled, result);
        Assert.Empty(dbContext.Users);
    }

    [Fact]
    public async Task Creates_first_admin_once_from_configuration()
    {
        const string password = "secure bootstrap phrase";
        await using var dbContext = CreateDbContext();
        var startupService = CreateService(dbContext, new Dictionary<string, string?>
        {
            ["AdminBootstrap:Enabled"] = "true",
            ["AdminBootstrap:FullName"] = "Render Demo Admin",
            ["AdminBootstrap:Email"] = "RENDER.ADMIN@example.com",
            ["AdminBootstrap:Password"] = password
        });

        var firstResult = await startupService.RunIfEnabledAsync(CancellationToken.None);
        var restartResult = await startupService.RunIfEnabledAsync(CancellationToken.None);

        var admin = Assert.Single(dbContext.Users);
        Assert.Equal(AdminBootstrapStartupResult.Created, firstResult);
        Assert.Equal(AdminBootstrapStartupResult.AdminAlreadyExists, restartResult);
        Assert.Equal(ApplicationRole.Admin, admin.Role);
        Assert.Equal("render.admin@example.com", admin.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash));
    }

    [Fact]
    public async Task Fails_closed_when_enabled_configuration_is_incomplete()
    {
        await using var dbContext = CreateDbContext();
        var startupService = CreateService(dbContext, new Dictionary<string, string?>
        {
            ["AdminBootstrap:Enabled"] = "true",
            ["AdminBootstrap:FullName"] = "Render Demo Admin",
            ["AdminBootstrap:Email"] = "render.admin@example.com"
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            startupService.RunIfEnabledAsync(CancellationToken.None));

        Assert.Contains("AdminBootstrap:Password", exception.Message);
        Assert.Empty(dbContext.Users);
    }

    [Fact]
    public async Task Does_not_require_or_reuse_bootstrap_secrets_when_admin_exists()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new AppUser
        {
            FullName = "Existing Admin",
            Email = "existing.admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("existing admin phrase"),
            Role = ApplicationRole.Admin,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
        var startupService = CreateService(dbContext, new Dictionary<string, string?>
        {
            ["AdminBootstrap:Enabled"] = "true"
        });

        var result = await startupService.RunIfEnabledAsync(CancellationToken.None);

        Assert.Equal(AdminBootstrapStartupResult.AdminAlreadyExists, result);
        Assert.Single(dbContext.Users);
        Assert.Equal("existing.admin@example.com", dbContext.Users.Single().Email);
    }

    [Fact]
    public async Task Configured_startup_bootstrap_allows_the_first_admin_to_log_in()
    {
        const string email = "render.admin@example.com";
        const string password = "secure bootstrap phrase";
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AdminBootstrap:Enabled"] = "true",
                        ["AdminBootstrap:FullName"] = "Render Demo Admin",
                        ["AdminBootstrap:Email"] = email,
                        ["AdminBootstrap:Password"] = password,
                        ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                        ["Jwt:Issuer"] = "AgriAssist",
                        ["Jwt:Audience"] = "AgriAssistUsers",
                        ["Jwt:ExpiryMinutes"] = "60"
                    });
                });
            });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(email, payload!.User.Email);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminBootstrapStartup-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static AdminBootstrapOnStartupService CreateService(
        AppDbContext dbContext,
        IDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var compromisedChecker = new ConfiguredCompromisedPasswordChecker(
            Options.Create(new PasswordSecurityOptions()));
        var passwordPolicy = new PasswordPolicyService(compromisedChecker);
        var bootstrapService = new AdminBootstrapService(dbContext, passwordPolicy);
        return new AdminBootstrapOnStartupService(configuration, bootstrapService);
    }
}
