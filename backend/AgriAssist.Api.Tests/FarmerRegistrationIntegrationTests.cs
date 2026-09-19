using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

public sealed class FarmerRegistrationIntegrationTests
{
    [Fact]
    public async Task Registration_assigns_farmer_role_even_when_admin_is_authenticated()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await TestUserSeeder.AddAdminAsync(factory);
        var adminLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@agriassist.local", "Admin@2026"));
        var adminSession = await adminLogin.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession!.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/auth/register-farmer",
            new
            {
                fullName = "New Farmer",
                email = "new.farmer@example.com",
                password = "harvest fields safely",
                role = 5
            });
        var session = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("authenticated", session?.AuthenticationStatus);
        Assert.Equal(ApplicationRole.Farmer, session?.User.Role);
        Assert.False(session!.User.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Email == "new.farmer@example.com");
        Assert.Equal(1, user.TokenVersion);
        Assert.NotNull(user.PasswordChangedAt);
        Assert.Null(user.CreatedByUserId);
    }

    [Fact]
    public async Task Duplicate_email_always_returns_standard_conflict()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = new RegisterFarmerRequest(
            "Duplicate Farmer",
            "duplicate@example.com",
            "harvest fields safely");

        var first = await client.PostAsJsonAsync("/api/auth/register-farmer", request);
        var duplicate = await client.PostAsJsonAsync("/api/auth/register-farmer", request);
        using var payload = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("EMAIL_ALREADY_EXISTS", payload.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Legacy_public_role_registration_endpoint_does_not_exist()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Unsafe Admin",
                email = "unsafe@example.com",
                password = "harvest fields safely",
                role = 5
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
