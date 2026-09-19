using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AgriAssist.Api.Configuration;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

public sealed class TemporaryPasswordFlowTests
{
    [Fact]
    public async Task Temporary_token_is_restricted_and_invalid_after_successful_change()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var userId = await AddTemporaryPasswordUserAsync(factory);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("temporary.officer@example.com", "temporary passphrase"));
        var temporarySession = await login.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("passwordChangeRequired", temporarySession?.AuthenticationStatus);
        Assert.Null(temporarySession?.AccessToken);
        Assert.NotNull(temporarySession?.PasswordChangeToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", temporarySession!.PasswordChangeToken);
        var forbiddenProfile = await client.GetAsync("/api/auth/profile");
        var forbiddenContent = await forbiddenProfile.Content.ReadAsStringAsync();
        using var forbiddenPayload = JsonDocument.Parse(forbiddenContent);
        Assert.True(
            forbiddenProfile.StatusCode == HttpStatusCode.Forbidden,
            $"Expected Forbidden but received {forbiddenProfile.StatusCode}: {forbiddenContent}");
        Assert.Equal(
            "PASSWORD_CHANGE_REQUIRED",
            forbiddenPayload.RootElement.GetProperty("error").GetProperty("code").GetString());

        var change = await client.PostAsJsonAsync(
            "/api/auth/change-temporary-password",
            new ChangeTemporaryPasswordRequest("new officer passphrase"));
        var normalSession = await change.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal("authenticated", normalSession?.AuthenticationStatus);
        Assert.NotNull(normalSession?.AccessToken);
        Assert.Null(normalSession?.PasswordChangeToken);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(normalSession!.AccessToken);
        Assert.Equal("2", jwt.Claims.Single(claim => claim.Type == AuthenticationClaimNames.TokenVersion).Value);
        Assert.Equal(
            AuthenticationTokenUses.Access,
            jwt.Claims.Single(claim => claim.Type == AuthenticationClaimNames.TokenUse).Value);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
            Assert.False(user.MustChangePassword);
            Assert.NotNull(user.PasswordChangedAt);
            Assert.Equal(2, user.TokenVersion);
            Assert.True(BCrypt.Net.BCrypt.Verify("new officer passphrase", user.PasswordHash));
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", temporarySession.PasswordChangeToken);
        var replay = await client.PostAsJsonAsync(
            "/api/auth/change-temporary-password",
            new ChangeTemporaryPasswordRequest("another officer phrase"));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    private static async Task<Guid> AddTemporaryPasswordUserAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser
        {
            FullName = "Temporary Officer",
            Email = "temporary.officer@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("temporary passphrase"),
            Role = ApplicationRole.FieldOfficer,
            IsActive = true,
            MustChangePassword = true,
            PasswordChangedAt = null,
            TokenVersion = 1
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                        ["Jwt:Issuer"] = "AgriAssist",
                        ["Jwt:Audience"] = "AgriAssistUsers",
                        ["Jwt:ExpiryMinutes"] = "60",
                        ["Jwt:PasswordChangeExpiryMinutes"] = "10"
                    });
                });
            });
}
