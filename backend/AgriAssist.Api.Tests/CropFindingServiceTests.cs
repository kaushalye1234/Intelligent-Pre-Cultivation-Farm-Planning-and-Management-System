using System.Net;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgriAssist.Api.Tests;

public sealed class CropFindingServiceTests
{
    [Fact]
    public async Task Suggestions_are_enriched_with_deterministic_existing_catalog_matches()
    {
        await using var db = NewDb();
        var rice = new CropType { Name = "  Rice  ", IsActive = true };
        var bg352 = new CropVariety { CropType = rice, Name = "BG  352", IsActive = true };
        db.AddRange(rice, bg352);
        await db.SaveChangesAsync();
        var ai = new FakeCropFindingAIClient
        {
            Crops = CropResponse(new CropSuggestion("crop-1", "rice", null, "Supported", "Explicitly listed.", [], [])),
            Varieties = VarietyResponse(rice.Id, rice.Name, new VarietySuggestion("variety-1", "bg 352", null, "Supported", "Explicitly listed.", [], []))
        };
        var service = NewService(db, ai);

        var cropResult = await service.SuggestCropsAsync(new SuggestCropsRequest(), CancellationToken.None);
        var varietyResult = await service.SuggestVarietiesAsync(new SuggestVarietiesRequest(rice.Id), CancellationToken.None);

        Assert.True(Assert.Single(cropResult.Suggestions).AlreadyExists);
        Assert.Equal(rice.Id, cropResult.Suggestions[0].ExistingCropTypeId);
        Assert.True(Assert.Single(varietyResult.Suggestions).AlreadyExists);
        Assert.Equal(bg352.Id, varietyResult.Suggestions[0].ExistingCropVarietyId);
        Assert.Equal("Rice", ai.LastVarietyInput!.CropName.Trim());
    }

    [Fact]
    public async Task Reference_discovery_marks_existing_source_and_filters_resource_rules()
    {
        await using var db = NewDb();
        var crop = new CropType { Name = "Rice", IsActive = true };
        var existing = new CropReferenceProfile
        {
            CropType = crop,
            SourceName = "DOA",
            SourceUrl = "https://doa.gov.lk/rice/",
            SourceVersion = "2026",
            VerifiedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Add(existing);
        await db.SaveChangesAsync();

        var source = Source("https://doa.gov.lk/rice");
        var allowed = Item("allowed", "region", "Sri Lanka");
        var prohibited = Item("blocked", "structuredRule", new { ruleType = "ResourceRequirement", fertilizerQuantity = 10 });
        var ai = new FakeCropFindingAIClient
        {
            References = new ReferenceDiscoveryResponse(
                "request-1", "DiscoverReferences", crop.Id, crop.Name, null, null, false,
                [new ReferenceSourceDraft(source, [allowed, prohibited], [], [])], [], [], [], [])
        };
        var service = NewService(db, ai);

        var result = await service.DiscoverReferencesAsync(new DiscoverReferencesRequest(crop.Id), CancellationToken.None);

        var draft = Assert.Single(result.SourceDrafts);
        Assert.True(draft.Source.ExistingReference);
        Assert.Equal(existing.Id, draft.Source.ExistingReferenceId);
        Assert.Equal("allowed", Assert.Single(draft.Items).Id);
        Assert.Contains(result.Warnings, warning => warning.Contains("resource or inventory", StringComparison.OrdinalIgnoreCase));
        Assert.Single(db.CropReferenceProfiles);
    }

    [Fact]
    public async Task Reference_discovery_rejects_variety_from_another_crop_before_AI_call()
    {
        await using var db = NewDb();
        var rice = new CropType { Name = "Rice" };
        var maize = new CropType { Name = "Maize" };
        var variety = new CropVariety { CropType = maize, Name = "Pacific" };
        db.AddRange(rice, maize, variety);
        await db.SaveChangesAsync();
        var ai = new FakeCropFindingAIClient();
        var service = NewService(db, ai);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            service.DiscoverReferencesAsync(new DiscoverReferencesRequest(rice.Id, variety.Id), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal("CROP_VARIETY_MISMATCH", error.Code);
        Assert.Null(ai.LastReferenceInput);
    }

    [Fact]
    public void Validators_enforce_typed_request_bounds()
    {
        Assert.NotEmpty(new SuggestCropsRequestValidator().Validate(new SuggestCropsRequest(new string('x', 301), 11)));
        Assert.NotEmpty(new SuggestVarietiesRequestValidator().Validate(new SuggestVarietiesRequest(Guid.Empty)));
        Assert.NotEmpty(new DiscoverReferencesRequestValidator().Validate(new DiscoverReferencesRequest(Guid.Empty, Guid.Empty, new string('x', 121))));
    }

    private static CropFindingService NewService(AppDbContext db, ICropFindingAIClient ai) =>
        new(
            db,
            new FixedCurrentUser(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SuggestCropsRequestValidator(),
            new SuggestVarietiesRequestValidator(),
            new DiscoverReferencesRequestValidator(),
            ai,
            NullLogger<CropFindingService>.Instance);

    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"crop-finding-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static CropSuggestionsResponse CropResponse(params CropSuggestion[] suggestions) =>
        new("request-1", "SuggestCrops", false, [], suggestions, [], [], []);

    private static VarietySuggestionsResponse VarietyResponse(Guid cropId, string cropName, params VarietySuggestion[] suggestions) =>
        new("request-1", "SuggestVarieties", cropId, cropName, false, [], suggestions, [], [], []);

    private static DiscoveredSource Source(string url) =>
        new("source-1", "Rice", "DOA", url, url, "Government", "Sri Lanka", "Sri Lankan", 1,
            "text/html", "Retrieved", DateTime.UtcNow.ToString("O"), "Approved source.", null, false, null, []);

    private static ReferenceDraftItem Item(string id, string field, object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return new ReferenceDraftItem(id, field, document.RootElement.Clone(), value.ToString() ?? "", "Supported", "Supported by source.", [], []);
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public ApplicationRole? Role => ApplicationRole.Admin;
        public bool IsInRole(ApplicationRole role) => role == ApplicationRole.Admin;
    }

    private sealed class FakeCropFindingAIClient : ICropFindingAIClient
    {
        public CropSuggestionsResponse Crops { get; init; } = CropResponse();
        public VarietySuggestionsResponse Varieties { get; init; } = VarietyResponse(Guid.NewGuid(), "Crop");
        public ReferenceDiscoveryResponse References { get; init; } = new(
            "request-1", "DiscoverReferences", Guid.NewGuid(), "Crop", null, null, false, [], [], [], [], []);
        public SuggestVarietiesInput? LastVarietyInput { get; private set; }
        public DiscoverReferencesInput? LastReferenceInput { get; private set; }

        public Task<CropSuggestionsResponse> SuggestCropsAsync(SuggestCropsInput input, CancellationToken cancellationToken) => Task.FromResult(Crops);
        public Task<VarietySuggestionsResponse> SuggestVarietiesAsync(SuggestVarietiesInput input, CancellationToken cancellationToken)
        {
            LastVarietyInput = input;
            return Task.FromResult(Varieties);
        }
        public Task<ReferenceDiscoveryResponse> DiscoverReferencesAsync(DiscoverReferencesInput input, CancellationToken cancellationToken)
        {
            LastReferenceInput = input;
            return Task.FromResult(References);
        }
    }
}
