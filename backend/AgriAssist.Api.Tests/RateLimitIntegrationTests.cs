using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Dtos.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgriAssist.Api.Tests;

public sealed class RateLimitIntegrationTests
{
    [Fact]
    public async Task Login_limit_returns_standard_error_and_retry_after()
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
                        ["Jwt:ExpiryMinutes"] = "60"
                    });
                });
            });
        using var client = factory.CreateClient();
        var request = new LoginRequest("missing@example.com", "not-the-password");

        var allowedResponses = new List<HttpResponseMessage>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            allowedResponses.Add(await client.PostAsJsonAsync("/api/auth/login", request));
        }

        var rejected = await client.PostAsJsonAsync("/api/auth/login", request);
        using var payload = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());

        Assert.All(allowedResponses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("RATE_LIMITED", payload.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.True(rejected.Headers.RetryAfter is not null);
    }
}
