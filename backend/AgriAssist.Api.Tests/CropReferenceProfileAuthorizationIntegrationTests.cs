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
    public async Task Draft_creation_and_edit_preserve_distinct_stage_and_rule_sources()
    {
        await using var factory = CreateFactory();
        var cropTypeId = await SeedCropTypeAsync(factory);
        using var officer = await ClientForAsync(factory, ApplicationRole.AgriculturalOfficer);
        var payload = new { cropTypeId, sourceName = "Synthetic overview", sourceUrl = "https://example.test/overview",
            sourceVersion = "fixture", stages = new[] { new { stageName = "Planting", sequence = 1,
                typicalMinDays = 3, typicalMaxDays = 5, sourceName = "Synthetic stage timetable", sourceUrl = "https://example.test/stages" } },
            rules = new[] { new { ruleType = "ResourceRequirement", ruleKey = "Urea",
                structuredValueJson = """{"resourceName":"Urea","quantityPerArea":1,"resourceUnit":"kg","areaUnit":"acre"}""",
                sourceName = "Synthetic resource rates", sourceUrl = "https://example.test/rates" } } };
        var createdResponse = await officer.PostAsJsonAsync("/api/crop-planning/crop-reference-profiles", payload);
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<CropReferenceProfileResponse>())!;
        var details = (await officer.GetFromJsonAsync<CropReferenceProfileDetailsResponse>("/api/crop-planning/crop-reference-profiles/" + created.Id))!;
        Assert.Equal("https://example.test/stages", details.Stages.Single().SourceUrl);
        Assert.Equal("https://example.test/rates", details.Rules.Single().SourceUrl);
        var edit = await officer.PutAsJsonAsync("/api/crop-planning/crop-reference-profiles/" + created.Id + "/draft",
            new { expectedDraftVersion = 1, profile = payload });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var updated = (await edit.Content.ReadFromJsonAsync<CropReferenceProfileDetailsResponse>())!;
        Assert.Equal("Synthetic stage timetable", updated.Stages.Single().SourceName);
        Assert.Equal("https://example.test/stages", updated.Stages.Single().SourceUrl);
        Assert.Equal("Synthetic resource rates", updated.Rules.Single().SourceName);
        Assert.Equal("https://example.test/rates", updated.Rules.Single().SourceUrl);
        Assert.False(updated.IsActive);
        Assert.Null(updated.VerifiedAt);
    }

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
            null,
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

    [Fact]
    public async Task Reference_creation_rejects_client_claimed_verification_time()
    {
        await using var factory = CreateFactory();
        var cropTypeId = await SeedCropTypeAsync(factory);
        using var officer = await ClientForAsync(factory, ApplicationRole.AgriculturalOfficer);
        var request = new CropReferenceProfileRequest(cropTypeId, null, "Anuradhapura", "Cited source",
            "https://example.test/source", "2026", DateTime.UtcNow.AddMinutes(-1),
            [new CropReferenceStageRequest("Maturity", 1, 98, 102, null)],
            [new CropReferenceRuleRequest("ResourceRequirement", "Urea", """{"resourceName":"Urea","quantityPerArea":1,"resourceUnit":"kg","areaUnit":"acre"}""")]);

        var response = await officer.PostAsJsonAsync("/api/crop-planning/crop-reference-profiles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reference_creation_without_verification_creates_inactive_draft()
    {
        await using var factory = CreateFactory();
        var cropTypeId = await SeedCropTypeAsync(factory);
        using var officer = await ClientForAsync(factory, ApplicationRole.AgriculturalOfficer);
        var request = new CropReferenceProfileRequest(cropTypeId, null, "Anuradhapura", "Cited source",
            "https://example.test/source", "2026", default,
            [new CropReferenceStageRequest("Maturity", 1, 98, 102, null)],
            [new CropReferenceRuleRequest("ResourceRequirement", "Urea", """{"resourceName":"Urea","quantityPerArea":1,"resourceUnit":"kg","areaUnit":"acre"}""")]);

        var response = await officer.PostAsJsonAsync("/api/crop-planning/crop-reference-profiles", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CropReferenceProfileResponse>();
        Assert.NotNull(created);
        Assert.False(created.IsActive);
    }
    [Fact]
    public async Task Same_agricultural_officer_can_verify_a_complete_draft_for_an_observed_field()
    {
        await using var factory = CreateFactory();
        var cropTypeId = await SeedCropTypeAsync(factory);
        Guid fieldId;
        Guid resourceId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = new AppUser { FullName = "Demo farmer", Email = $"farmer-{Guid.NewGuid():N}@example.test", PasswordHash = "test", Role = ApplicationRole.Farmer };
            var farm = new Farm { Name = "Demo farm", Location = "Anuradhapura, Sri Lanka", District = "Anuradhapura", TotalArea = 1, OwnerUser = owner };
            var field = new Field { Name = "Demo field", Area = 1, SoilType = "Loam", Farm = farm };
            var category = new AgriAssist.Api.Models.Resources.ResourceCategory { Name = "Fertilizer" };
            var resource = new AgriAssist.Api.Models.Resources.Resource { Name = "Urea", Unit = "kg", ResourceCategory = category };
            db.AddRange(owner, farm, field, category, resource);
            await db.SaveChangesAsync();
            fieldId = field.Id;
            resourceId = resource.Id;
        }
        using var officer = await ClientForAsync(factory, ApplicationRole.AgriculturalOfficer);
        var draft = await officer.PostAsJsonAsync("/api/crop-planning/crop-reference-profiles",
            new CropReferenceProfileRequest(cropTypeId, null, "Anuradhapura", "Department of Agriculture",
                "https://example.test/rice", "2026", null,
                [new CropReferenceStageRequest("Maturity", 1, 98, 102, "Source review required")],
                [new CropReferenceRuleRequest("ResourceRequirement", "Urea",
                    $"{{\"resourceId\":\"{resourceId}\",\"resourceName\":\"Urea\",\"quantityPerArea\":1,\"resourceUnit\":\"kg\",\"areaUnit\":\"acre\"}}")]));
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var profile = await draft.Content.ReadFromJsonAsync<CropReferenceProfileResponse>();
        Assert.NotNull(profile);
        Assert.False(profile.IsActive);
        var edited = await officer.PutAsJsonAsync($"/api/crop-planning/crop-reference-profiles/{profile.Id}/draft",
            new { expectedDraftVersion = 1, profile = new CropReferenceProfileRequest(cropTypeId, null,
                "Anuradhapura", "Department of Agriculture", "https://example.test/rice", "2026 reviewed", null,
                [new CropReferenceStageRequest("Maturity", 1, 98, 102, "Officer reviewed the cited source")],
                [new CropReferenceRuleRequest("ResourceRequirement", "Urea",
                    $"{{\"resourceId\":\"{resourceId}\",\"resourceName\":\"Urea\",\"quantityPerArea\":1,\"resourceUnit\":\"kg\",\"areaUnit\":\"acre\"}}")]) });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var updatedDraft = await edited.Content.ReadFromJsonAsync<CropReferenceProfileDetailsResponse>();
        Assert.Equal(2, updatedDraft!.DraftVersion);
        var stale = await officer.PostAsJsonAsync($"/api/crop-planning/crop-reference-profiles/{profile.Id}/verify",
            new { fieldId, waterRegime = WaterRegime.Irrigated, observation = "Officer confirmed irrigation.", expectedDraftVersion = 1, confirmed = true });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var verified = await officer.PostAsJsonAsync($"/api/crop-planning/crop-reference-profiles/{profile.Id}/verify",
            new { fieldId, waterRegime = WaterRegime.Irrigated, observation = "Officer confirmed controlled irrigation at this field.", expectedDraftVersion = 2, confirmed = true });

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var result = await verified.Content.ReadFromJsonAsync<CropReferenceProfileDetailsResponse>();
        Assert.NotNull(result);
        Assert.Equal(CropReferenceVerificationState.Verified, result.VerificationState);
        Assert.True(result.IsActive);
        Assert.NotNull(result.VerifiedAt);
        Assert.NotNull(result.VerifiedByUserId);
        Assert.Equal(WaterRegime.Irrigated, result.WaterRegime);
        await using var check = factory.Services.CreateAsyncScope();
        var dbCheck = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(dbCheck.FieldWaterRegimeVerifications.Where(item => item.FieldId == fieldId));
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
