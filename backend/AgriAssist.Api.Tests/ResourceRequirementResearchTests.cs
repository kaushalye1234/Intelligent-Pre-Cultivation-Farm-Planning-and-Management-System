using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using AgriAssist.Api.Validators.Resources;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

/// <summary>
/// Member 3 Admin-only Resource Requirement Research. The rates used here are SAMPLE TEST DATA ONLY; they are not
/// agronomic recommendations. Research must never save; only an Admin "Verify &amp; Save" stores a rule, and the
/// stored rule must be what GetCropResourceRequirements (and so the WeatherResourceAgent) reads.
/// </summary>
public sealed class ResourceRequirementResearchTests
{
    private const string SourceUrl = "https://doa.gov.lk/fcrdi-crops/";

    [Fact]
    public async Task Research_sends_database_names_and_returns_an_unverified_draft_without_saving()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        db.CropTypes.Add(new CropType { Name = "Onion", IsActive = true });
        await db.SaveChangesAsync();
        var ai = new StubAiClient(input => Draft(input, "PendingVerification", 80m, verified: true));

        var result = await Service(db, ai).ResearchAsync(new ResourceRequirementResearchRequest(data.CropTypeId, data.ResourceId), CancellationToken.None);

        Assert.Equal("PendingVerification", result.Status);
        Assert.False(result.Verified);
        Assert.Equal(80m, result.SuggestedQuantityPerArea);
        Assert.Equal(("Tomato", "Urea", "kg"), (ai.Input!.CropName, ai.Input.ResourceName, ai.Input.ResourceUnit));
        Assert.Equal(new[] { "Onion" }, ai.Input.OtherCropNames);
        Assert.Empty(db.CropReferenceProfiles);
        Assert.Empty(db.CropRuleReferences);
    }

    [Fact]
    public async Task Unknown_crop_or_resource_is_rejected_before_any_research()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        var ai = new StubAiClient(input => Draft(input, "PendingVerification", 80m));

        var crop = await Assert.ThrowsAsync<ApiException>(() =>
            Service(db, ai).ResearchAsync(new ResourceRequirementResearchRequest(Guid.NewGuid(), data.ResourceId), CancellationToken.None));
        var resource = await Assert.ThrowsAsync<ApiException>(() =>
            Service(db, ai).ResearchAsync(new ResourceRequirementResearchRequest(data.CropTypeId, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal((HttpStatusCode.NotFound, "Crop type was not found."), (crop.StatusCode, crop.Message));
        Assert.Equal((HttpStatusCode.NotFound, "Resource was not found."), (resource.StatusCode, resource.Message));
        Assert.Null(ai.Input);
    }

    [Fact]
    public async Task Inactive_resource_is_rejected()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        (await db.Resources.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();
        var ai = new StubAiClient(input => Draft(input, "PendingVerification", 80m));

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            Service(db, ai).ResearchAsync(new ResourceRequirementResearchRequest(data.CropTypeId, data.ResourceId), CancellationToken.None));

        Assert.Equal((HttpStatusCode.BadRequest, "RESOURCE_INACTIVE"), (error.StatusCode, error.Code));
        Assert.Null(ai.Input);
    }

    [Fact]
    public async Task Non_admin_cannot_research_or_save()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        var service = Service(db, new StubAiClient(input => Draft(input, "PendingVerification", 80m)), ApplicationRole.ResourceOfficer);

        var research = await Assert.ThrowsAsync<ApiException>(() =>
            service.ResearchAsync(new ResourceRequirementResearchRequest(data.CropTypeId, data.ResourceId), CancellationToken.None));
        var save = await Assert.ThrowsAsync<ApiException>(() => service.VerifyAndSaveAsync(Verify(data, 80m), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, research.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
        Assert.Empty(db.CropRuleReferences);
    }

    [Theory]
    [InlineData("NoVerifiedRecommendationFound")]
    [InlineData("ConflictingSources")]
    [InlineData("EvidenceValidationFailed")]
    public async Task Results_without_a_single_verified_value_never_carry_a_suggested_quantity(string status)
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        // The stub misbehaves on purpose: it attaches a value to a non-pending status.
        var ai = new StubAiClient(input => Draft(input, status, 80m));

        var result = await Service(db, ai).ResearchAsync(new ResourceRequirementResearchRequest(data.CropTypeId, data.ResourceId), CancellationToken.None);

        Assert.Equal(status, result.Status);
        Assert.Null(result.SuggestedQuantityPerArea);
        Assert.Null(result.SuggestedAreaUnit);
        Assert.Empty(db.CropRuleReferences);
    }

    [Fact]
    public async Task Conflicting_sources_are_returned_with_every_recommendation()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        var ai = new StubAiClient(input => Draft(input, "ConflictingSources", null) with
        {
            Recommendations = [Recommendation(195m), Recommendation(150m)]
        });

        var result = await Service(db, ai).ResearchAsync(new ResourceRequirementResearchRequest(data.CropTypeId, data.ResourceId), CancellationToken.None);

        Assert.Equal(new[] { 195m, 150m }, result.Recommendations.Select(item => item.QuantityPerArea));
        Assert.Null(result.SuggestedQuantityPerArea);
    }

    [Fact]
    public async Task Ai_failures_map_to_safe_errors_and_save_nothing()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null);
        var request = new ResourceRequirementResearchRequest(data.CropTypeId, data.ResourceId);
        var timeoutDetail = new CropFindingErrorDetail("CROP_FINDING_TIMEOUT", "Timed out.", "req-1", "web_search", 1, 1, "timeout", null, null, null, null, null);

        var timeout = await Assert.ThrowsAsync<ApiException>(() => Service(db, new StubAiClient(_ => throw new CropFindingAIException(HttpStatusCode.GatewayTimeout, timeoutDetail))).ResearchAsync(request, CancellationToken.None));
        var unconfigured = await Assert.ThrowsAsync<ApiException>(() => Service(db, new StubAiClient(_ => throw new CropFindingAIException(HttpStatusCode.ServiceUnavailable, null))).ResearchAsync(request, CancellationToken.None));
        var unreachable = await Assert.ThrowsAsync<ApiException>(() => Service(db, new StubAiClient(_ => throw new HttpRequestException("connection refused"))).ResearchAsync(request, CancellationToken.None));
        var wrongResource = await Assert.ThrowsAsync<ApiException>(() => Service(db, new StubAiClient(input => Draft(input, "PendingVerification", 80m) with { ResourceId = Guid.NewGuid() })).ResearchAsync(request, CancellationToken.None));

        Assert.Equal((HttpStatusCode.GatewayTimeout, "RESOURCE_RESEARCH_TIMEOUT"), (timeout.StatusCode, timeout.Code));
        Assert.Equal((HttpStatusCode.ServiceUnavailable, "RESOURCE_RESEARCH_CONFIGURATION_UNAVAILABLE"), (unconfigured.StatusCode, unconfigured.Code));
        Assert.Equal((HttpStatusCode.BadGateway, "RESOURCE_RESEARCH_UNAVAILABLE"), (unreachable.StatusCode, unreachable.Code));
        Assert.Equal((HttpStatusCode.BadGateway, "RESOURCE_RESEARCH_INVALID"), (wrongResource.StatusCode, wrongResource.Code));
        Assert.DoesNotContain("connection refused", unreachable.Message);
        Assert.Empty(db.CropRuleReferences);
    }

    [Fact]
    public async Task Admin_research_saves_an_inactive_draft_that_member_3_does_not_consume()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, ruleJson: null, fieldArea: 0.5m);

        var saved = await Service(db).VerifyAndSaveAsync(Verify(data, 80m), CancellationToken.None);

        Assert.True(saved.CreatedReferenceProfile);
        Assert.Equal(CropReferenceVerificationState.Draft, saved.VerificationState);
        Assert.Null(saved.VerifiedAt);
        var profile = await db.CropReferenceProfiles.SingleAsync();
        Assert.False(profile.IsActive);
        Assert.Null(profile.VerifiedAt);
        var rule = await db.CropRuleReferences.SingleAsync();
        Assert.Equal(("ResourceRequirement", "urea", SourceUrl), (rule.RuleType, rule.RuleKey, rule.SourceUrl));
        Assert.Contains("AdminReviewedWebResearchDraft", rule.StructuredValueJson);
        var requirements = await new CropResourceRequirementService(db, TestConfiguration()).GetRequirementsAsync(data.RequestId, CancellationToken.None);
        Assert.Equal("Unavailable", requirements.Status);
    }

    [Fact]
    public async Task Admin_research_preserves_the_active_profile_and_copies_its_stages_into_a_new_draft()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db, fieldArea: 0.5m);
        var profile = await db.CropReferenceProfiles.Include(item => item.Rules).SingleAsync();
        db.CropStageReferences.Add(new CropStageReference { CropReferenceProfileId = profile.Id, StageName = "Flowering", Sequence = 1, SourceName = "SAMPLE stage source", SourceUrl = SourceUrl });
        await db.SaveChangesAsync();
        var previousRuleId = profile.Rules.Single().Id;

        var saved = await Service(db).VerifyAndSaveAsync(Verify(data, 80m), CancellationToken.None);

        Assert.NotEqual(profile.Id, saved.CropReferenceProfileId);
        Assert.True(profile.IsActive);
        Assert.False((await db.CropRuleReferences.SingleAsync(rule => rule.Id == previousRuleId)).IsDeleted);
        var draft = await db.CropReferenceProfiles.Include(item => item.Stages).Include(item => item.Rules)
            .SingleAsync(item => item.Id == saved.CropReferenceProfileId);
        Assert.False(draft.IsActive);
        Assert.Equal("SAMPLE stage source", Assert.Single(draft.Stages).SourceName);
        Assert.Equal(80m, Assert.Single(draft.Rules.Where(rule => rule.RuleType == "ResourceRequirement")) is { } rule
            && CropResourceRequirementRule.TryParse(rule.StructuredValueJson, out var parsed, out _) ? parsed!.QuantityPerArea : 0m);
        var requirement = Assert.Single((await new CropResourceRequirementService(db, TestConfiguration()).GetRequirementsAsync(data.RequestId, CancellationToken.None)).Requirements);
        Assert.Equal(50m, requirement.RequiredQuantity);
    }

    [Fact]
    public async Task Verify_rejects_a_unit_that_differs_from_inventory_or_a_region_that_would_hide_the_existing_reference()
    {
        await using var db = NewDbContext();
        var data = await SeedAsync(db); // existing generic profile

        var unit = await Assert.ThrowsAsync<ApiException>(() => Service(db).VerifyAndSaveAsync(Verify(data, 80m) with { ResourceUnit = "bag" }, CancellationToken.None));
        var region = await Assert.ThrowsAsync<ApiException>(() => Service(db).VerifyAndSaveAsync(Verify(data, 80m) with { Region = "Jaffna" }, CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<ApiException>(() => Service(db).VerifyAndSaveAsync(Verify(data, 0m) with { SourceUrl = "javascript:alert(1)" }, CancellationToken.None));

        Assert.Equal((HttpStatusCode.BadRequest, "RESOURCE_UNIT_MISMATCH"), (unit.StatusCode, unit.Code));
        Assert.Equal((HttpStatusCode.Conflict, "REFERENCE_PROFILE_REGION_MISMATCH"), (region.StatusCode, region.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "VALIDATION_ERROR"), (invalid.StatusCode, invalid.Code));
        Assert.Contains("Source URL", invalid.Message);
        Assert.Single(db.CropRuleReferences);
        Assert.False((await db.CropRuleReferences.SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task Endpoints_are_admin_only_and_never_expose_service_tokens()
    {
        const string serviceToken = "test-ai-service-token-value";
        const string toolToken = "test-ai-tool-token-value";
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                ["Jwt:Issuer"] = "AgriAssist",
                ["Jwt:Audience"] = "AgriAssistUsers",
                ["Jwt:ExpiryMinutes"] = "60",
                ["AI:ServiceToken"] = serviceToken,
                ["AI:ToolToken"] = toolToken
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IResourceRequirementResearchAIClient>();
                services.AddSingleton<IResourceRequirementResearchAIClient>(new StubAiClient(input => Draft(input, "PendingVerification", 80m)));
            });
        });
        Guid cropId, resourceId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var crop = new CropType { Name = $"Chili {Guid.NewGuid():N}", IsActive = true };
            var resource = new Resource { Name = "Urea", Unit = "kg", ResourceCategory = new ResourceCategory { Name = "Fertilizer" } };
            db.AddRange(crop, resource);
            await db.SaveChangesAsync();
            (cropId, resourceId) = (crop.Id, resource.Id);
        }
        const string path = "/api/resources/requirement-research";
        var research = new ResourceRequirementResearchRequest(cropId, resourceId);
        var verify = new VerifyResourceRequirementRequest(cropId, resourceId, 80m, "kg", "acre", "Department of Agriculture", SourceUrl, "Urea 80 kg/ac");

        using var anonymous = factory.CreateClient();
        using var farmer = await ClientForAsync(factory, ApplicationRole.Farmer);
        using var officer = await ClientForAsync(factory, ApplicationRole.ResourceOfficer);
        using var admin = await ClientForAsync(factory, ApplicationRole.Admin);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, research)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PostAsJsonAsync(path, research)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.PostAsJsonAsync(path, research)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.PostAsJsonAsync($"{path}/verify", verify)).StatusCode);

        var response = await admin.PostAsJsonAsync(path, research);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"verified\":false", body);
        Assert.DoesNotContain(serviceToken, body);
        Assert.DoesNotContain(toolToken, body);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().CropRuleReferences);
        }

        var saved = await admin.PostAsJsonAsync($"{path}/verify", verify);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.DoesNotContain(serviceToken, await saved.Content.ReadAsStringAsync());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Single(scope.ServiceProvider.GetRequiredService<AppDbContext>().CropRuleReferences);
        }
    }

    private static VerifyResourceRequirementRequest Verify(Seeded data, decimal quantity) =>
        new(data.CropTypeId, data.ResourceId, quantity, "kg", "acre", "Department of Agriculture (SAMPLE)", SourceUrl, $"Urea {quantity} kg/ac");

    private static ResourceRequirementResearchResponse Draft(ResourceRequirementResearchInput input, string status, decimal? quantity, bool verified = false) =>
        new("request-1", status, verified, input.CropTypeId, input.CropName, input.CropVarietyId, input.VarietyName, input.Region,
            input.ResourceId, input.ResourceName, input.ResourceUnit, quantity, quantity is null ? null : "kg", quantity is null ? null : "acre",
            "Department of Agriculture (SAMPLE)", SourceUrl, $"Urea {quantity} kg/ac", false,
            quantity is null ? Array.Empty<ResourceRequirementRecommendation>() : new[] { Recommendation(quantity.Value) }, [], [], []);

    private static ResourceRequirementRecommendation Recommendation(decimal quantity) =>
        new($"local-{quantity}", quantity, "kg", "acre", true, $"{quantity} kg/acre", [new ResourceRequirementComponent("Season", quantity, $"Urea {quantity} kg/ac")],
            "Supported", "", new EvidenceProvenance("local", "Guide", "Department of Agriculture", SourceUrl, SourceUrl, "Government", "Sri Lanka", "Sri Lankan", 1, $"Urea {quantity} kg/ac", null, null), []);

    private static ResourceRequirementResearchService Service(AppDbContext db, IResourceRequirementResearchAIClient? ai = null, ApplicationRole role = ApplicationRole.Admin)
    {
        var user = new StubCurrentUser(role);
        return new ResourceRequirementResearchService(
            db,
            user,
            new ResourceRequirementResearchRequestValidator(),
            new VerifyResourceRequirementRequestValidator(),
            ai ?? new StubAiClient(_ => throw new InvalidOperationException("Research is not used by this test.")),
            NullLogger<ResourceRequirementResearchService>.Instance);
    }

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, ApplicationRole role)
    {
        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@requirement.test";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new AppUser
            {
                FullName = $"Requirement {role}",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Requirement@2026"),
                Role = role,
                IsActive = true,
                MustChangePassword = false,
                PasswordChangedAt = DateTime.UtcNow,
                TokenVersion = 1
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Requirement@2026"));
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private sealed class StubAiClient(Func<ResourceRequirementResearchInput, ResourceRequirementResearchResponse> respond) : IResourceRequirementResearchAIClient
    {
        public ResourceRequirementResearchInput? Input { get; private set; }

        public Task<ResourceRequirementResearchResponse> ResearchResourceRequirementAsync(ResourceRequirementResearchInput input, CancellationToken cancellationToken)
        {
            Input = input;
            return Task.FromResult(respond(input));
        }
    }

    private sealed class StubCurrentUser(ApplicationRole role) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public ApplicationRole? Role { get; } = role;
        public bool IsInRole(ApplicationRole roleToCheck) => Role == roleToCheck;
    }
}
