using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

public sealed class CropPlanningWorkflowAuthorizationIntegrationTests
{
    [Fact]
    public async Task Start_ai_workflow_is_admin_only()
    {
        await using var factory = CreateFactory();
        var requestId = Guid.NewGuid();
        using var anonymous = factory.CreateClient();
        using var farmer = await ClientForAsync(factory, ApplicationRole.Farmer);
        using var agriculturalOfficer = await ClientForAsync(factory, ApplicationRole.AgriculturalOfficer);
        using var admin = await ClientForAsync(factory, ApplicationRole.Admin);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync($"/api/crop-plans/{requestId}/start-ai-workflow", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await farmer.PostAsJsonAsync($"/api/crop-plans/{requestId}/start-ai-workflow", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await agriculturalOfficer.PostAsJsonAsync($"/api/crop-plans/{requestId}/start-ai-workflow", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsJsonAsync($"/api/crop-plans/{requestId}/start-ai-workflow", new { })).StatusCode);
    }

    [Fact]
    public async Task Recovery_actions_enforce_admin_and_officer_roles()
    {
        await using var factory = CreateFactory();
        var id = Guid.NewGuid();
        using var anonymous = factory.CreateClient();
        using var farmer = await ClientForAsync(factory, ApplicationRole.Farmer);
        using var officer = await ClientForAsync(factory, ApplicationRole.AgriculturalOfficer);
        using var admin = await ClientForAsync(factory, ApplicationRole.Admin);
        var replacement = new { blockedWorkflowId = Guid.NewGuid(), verifiedProfileId = Guid.NewGuid(), idempotencyKey = Guid.NewGuid() };
        var verification = new { fieldId = Guid.NewGuid(), waterRegime = 1, observation = "Synthetic observation", expectedDraftVersion = 1, confirmed = true };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/crop-plans/{id}/replace-blocked-workflow", replacement)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PostAsJsonAsync($"/api/crop-plans/{id}/replace-blocked-workflow", replacement)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync($"/api/crop-plans/{id}/replace-blocked-workflow", replacement)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/crop-plans/{id}/replace-blocked-workflow", replacement)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/crop-planning/crop-reference-profiles/{id}/verify", verification)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await officer.PostAsJsonAsync($"/api/crop-planning/crop-reference-profiles/{id}/verify", verification)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.GetAsync($"/api/task-approval/workflows/{id}/evidence-resolution")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await officer.GetAsync($"/api/task-approval/workflows/{id}/evidence-resolution")).StatusCode);
    }

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, ApplicationRole role)
    {
        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@cropplanning.test";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new AppUser
            {
                FullName = $"Crop Planning {role}",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("CropPlanning@2026"),
                Role = role,
                IsActive = true,
                MustChangePassword = false,
                PasswordChangedAt = DateTime.UtcNow,
                TokenVersion = 1
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "CropPlanning@2026"));
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                ["Jwt:Issuer"] = "AgriAssist",
                ["Jwt:Audience"] = "AgriAssistUsers",
                ["Jwt:ExpiryMinutes"] = "60"
            }));
        });
}
