$authIntegrationTests = @'
using System.Net;
using System.Net.Http.Json;
using AgriAssist.Api.Dtos.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgriAssist.Api.Tests;

public sealed class AuthIntegrationTests
{
    [Fact]
    public async Task Login_returns_token_and_profile_endpoint_requires_token()
    {
        await using var factory = new WebApplicationFactory<Program>()
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
                        ["App:ReactUrl"] = "http://localhost:5173"
                    });
                });
            });
        using var client = factory.CreateClient();

        var unauthorized = await client.GetAsync("/api/auth/profile");
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin@agriassist.local", "Admin@2026"));
        var payload = await login.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.Equal("admin@agriassist.local", payload.User.Email);
    }
}
'@

Set-Content -LiteralPath "backend/AgriAssist.Api.Tests/AuthIntegrationTests.cs" -Value $authIntegrationTests -Encoding ASCII
