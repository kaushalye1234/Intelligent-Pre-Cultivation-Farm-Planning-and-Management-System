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

public sealed class JwtSecurityValidationTests
{
    [Theory]
    [InlineData("version")]
    [InlineData("role")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    public async Task Current_database_security_state_invalidates_existing_token(string mutation)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await TestUserSeeder.AddAdminAsync(factory);
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin@agriassist.local", "Admin@2026"));
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.SingleAsync(item => item.Email == "admin@agriassist.local");
            switch (mutation)
            {
                case "version":
                    user.TokenVersion++;
                    break;
                case "role":
                    user.Role = ApplicationRole.FieldOfficer;
                    break;
                case "inactive":
                    user.IsActive = false;
                    break;
                case "deleted":
                    user.IsDeleted = true;
                    break;
            }

            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var profile = await client.GetAsync("/api/auth/profile");
        using var payload = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        Assert.Equal(
            "SESSION_SECURITY_VERSION_INVALID",
            payload.RootElement.GetProperty("error").GetProperty("code").GetString());
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
                        ["Jwt:ExpiryMinutes"] = "60"
                    });
                });
            });
}
