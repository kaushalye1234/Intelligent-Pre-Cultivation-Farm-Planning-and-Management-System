using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

public sealed class CropReferenceProfileAuthorizationIntegrationTests
{
    [Fact]
    public async Task Agricultural_officer_can_manage_verified_references_without_receiving_catalog_admin_access()
    {
        await using var factory = CreateFactory();
        var cropTypeId = await SeedCropTypeAsync(factory);
        using var anonymous = factory.CreateClient();
        using var farmer = await ClientForAsync(factory, ApplicationRole.Farmer);
        using var fieldOfficer = await ClientForAsync(factory, ApplicationRole.FieldOfficer);
        using var resourceOfficer = await ClientForAsync(factory, ApplicationRole.ResourceOfficer);
        using var agriculturalOfficer = await ClientForAsync(factory, ApplicationRole.AgriculturalOfficer);
        using var admin = await ClientForAsync(factory, ApplicationRole.Admin);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/crop-planning/crop-reference-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await farmer.GetAsync("/api/crop-planning/crop-reference-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fieldOfficer.GetAsync("/api/crop-planning/crop-reference-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await resourceOfficer.GetAsync("/api/crop-planning/crop-reference-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.GetAsync("/api/crop-planning/crop-reference-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await agriculturalOfficer.GetAsync("/api/crop-planning/crop-reference-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await agriculturalOfficer.GetAsync("/api/crop-planning/crop-types?includeInactive=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await agriculturalOfficer.GetAsync($"/api/crop-planning/crop-varieties?cropTypeId={cropTypeId}&includeInactive=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.GetAsync("/api/crop-planning/crop-types?includeInactive=true")).StatusCode);

        var request = new CropReferenceProfileRequest(
            cropTypeId,
            null,
            "Kurunegala",
            "Department of Agriculture field guide",
            "https://example.test/rice-guide",
            "2026 edition",
            DateTime.UtcNow.AddMinutes(-5),
            [new CropReferenceStageRequest("Land preparation", 1, 3, 7, "Verified from the cited guide.")],
            []);

        var createResponse = await agriculturalOfficer.PostAsJsonAsync(
            "/api/crop-planning/crop-reference-profiles",
            request);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CropReferenceProfileResponse>();
        Assert.NotNull(created);

        Assert.Equal(HttpStatusCode.OK,
            (await agriculturalOfficer.GetAsync($"/api/crop-planning/crop-reference-profiles/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await agriculturalOfficer.PutAsJsonAsync(
                $"/api/crop-planning/crop-reference-profiles/{created.Id}/active",
                false)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await agriculturalOfficer.PostAsJsonAsync(
                "/api/crop-planning/crop-types",
                new CropTypeRequest("Unauthorized crop", null, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await agriculturalOfficer.PostAsJsonAsync(
                "/api/crop-planning/crop-varieties",
                new CropVarietyRequest(cropTypeId, "Unauthorized variety", true))).StatusCode);
    }

    private static async Task<Guid> SeedCropTypeAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var crop = new CropType { Name = $"Rice {Guid.NewGuid():N}", IsActive = true };
        db.CropTypes.Add(crop);
        await db.SaveChangesAsync();
        return crop.Id;
    }

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, ApplicationRole role)
    {
        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@references.test";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new AppUser
            {
                FullName = $"Reference Manager {role}",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("References@2026"),
                Role = role,
                IsActive = true,
                MustChangePassword = false,
                PasswordChangedAt = DateTime.UtcNow,
                TokenVersion = 1
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "References@2026"));
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
