using System.Net;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Resources;

public interface IResourceRequirementResearchService
{
    Task<ResourceRequirementResearchResponse> ResearchAsync(ResourceRequirementResearchRequest request, CancellationToken cancellationToken);
    Task<VerifiedResourceRequirementResponse> VerifyAndSaveAsync(VerifyResourceRequirementRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Admin-only Member 3 Resource Requirement Research. ResearchAsync asks the ai-service, which searches only the
/// approved agricultural sources, and returns an unverified draft; it never writes to the database.
/// VerifyAndSaveAsync stores the value an Admin checked as a ResourceRequirement rule in the existing crop reference
/// data, which GetCropResourceRequirements and the WeatherResourceAgent already read. Nothing else changes.
/// </summary>
public sealed class ResourceRequirementResearchService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IRequestValidator<ResourceRequirementResearchRequest> researchValidator,
    IRequestValidator<VerifyResourceRequirementRequest> verifyValidator,
    IResourceRequirementResearchAIClient aiClient,
    ILogger<ResourceRequirementResearchService> logger) : IResourceRequirementResearchService
{
    public const string VerificationMethod = "AdminReviewedWebResearchDraft";
    public const string PendingVerification = "PendingVerification";
    private const int MaxOtherCropNames = 200;
    private static readonly HashSet<string> ResearchStatuses =
        [PendingVerification, "ConflictingSources", "NoVerifiedRecommendationFound", "EvidenceValidationFailed"];

    public async Task<ResourceRequirementResearchResponse> ResearchAsync(
        ResourceRequirementResearchRequest request,
        CancellationToken cancellationToken)
    {
        Validate(researchValidator.Validate(request));
        var adminId = RequireAdmin();
        var crop = await LoadCropAsync(request.CropTypeId, cancellationToken);
        var varietyName = await LoadVarietyNameAsync(crop.Id, request.CropVarietyId, cancellationToken);
        var resource = await LoadResourceAsync(request.ResourceId, cancellationToken);
        var otherCrops = await dbContext.CropTypes.AsNoTracking()
            .Where(item => item.Id != crop.Id && !item.IsDeleted)
            .OrderBy(item => item.Name)
            .Select(item => item.Name)
            .Take(MaxOtherCropNames)
            .ToListAsync(cancellationToken);

        // Names and unit come from the database, never from the client.
        var input = new ResourceRequirementResearchInput(
            adminId, crop.Id, crop.Name, request.CropVarietyId, varietyName, CleanOptional(request.Region),
            resource.Id, resource.Name, resource.Unit, otherCrops);
        var response = await CallAsync(() => aiClient.ResearchResourceRequirementAsync(input, cancellationToken), cancellationToken);

        if (response.CropTypeId != crop.Id || response.ResourceId != resource.Id || !ResearchStatuses.Contains(response.Status))
        {
            logger.LogWarning(
                "Resource requirement research request {RequestId} returned an unexpected crop, resource or status {Status}",
                response.RequestId, response.Status);
            throw new ApiException(HttpStatusCode.BadGateway, "RESOURCE_RESEARCH_INVALID",
                "The research service returned an unexpected result. Nothing was saved.");
        }

        var pending = response.Status == PendingVerification && response.SuggestedQuantityPerArea > 0;
        // A research result is a draft: it is never verified here, and only a pending result carries a suggested value.
        return response with
        {
            Verified = false,
            SuggestedQuantityPerArea = pending ? response.SuggestedQuantityPerArea : null,
            SuggestedResourceUnit = pending ? response.SuggestedResourceUnit : null,
            SuggestedAreaUnit = pending ? response.SuggestedAreaUnit : null
        };
    }

    public async Task<VerifiedResourceRequirementResponse> VerifyAndSaveAsync(
        VerifyResourceRequirementRequest request,
        CancellationToken cancellationToken)
    {
        Validate(verifyValidator.Validate(request));
        var adminId = RequireAdmin();
        var crop = await LoadCropAsync(request.CropTypeId, cancellationToken);
        var varietyName = await LoadVarietyNameAsync(crop.Id, request.CropVarietyId, cancellationToken);
        var resource = await LoadResourceAsync(request.ResourceId, cancellationToken);
        if (!string.Equals(request.ResourceUnit.Trim(), resource.Unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            // The WeatherResourceAgent compares the requirement with stock, so the units must be identical.
            throw new ApiException(HttpStatusCode.BadRequest, "RESOURCE_UNIT_MISMATCH",
                $"The requirement is in {request.ResourceUnit.Trim()} but {resource.Name} is stocked in {resource.Unit}. Convert it to {resource.Unit} before saving.");
        }

        var areaUnit = CropResourceRequirementRule.NormalizeAreaUnit(request.AreaUnit)!;
        var region = CleanOptional(request.Region);
        var sourceName = request.SourceName.Trim();
        var sourceUrl = request.SourceUrl.Trim();
        var structuredValueJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["resourceName"] = resource.Name.Trim(),
            ["resourceId"] = resource.Id.ToString(),
            ["quantityPerArea"] = request.QuantityPerArea,
            ["resourceUnit"] = resource.Unit.Trim(),
            ["areaUnit"] = areaUnit,
            ["verificationMethod"] = VerificationMethod,
            ["evidence"] = request.Evidence.Trim(),
            ["researchRequestId"] = CleanOptional(request.ResearchRequestId)
        });
        // Same format check the reference-profile validator applies to every ResourceRequirement rule.
        if (!CropResourceRequirementRule.TryParse(structuredValueJson, out _, out var ruleError))
            throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", $"Resource requirement rule: {ruleError}");

        var ruleKey = RuleKeyFor(resource.Name);
        var now = DateTime.UtcNow;
        var candidates = (await dbContext.CropReferenceProfiles
                .Include(profile => profile.Rules).Include(profile => profile.Stages)
                .Where(profile => profile.CropTypeId == crop.Id && profile.IsActive && !profile.IsDeleted && profile.VerifiedAt <= now)
                .ToListAsync(cancellationToken))
            .Where(profile => string.Equals(profile.VarietyName, varietyName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var target = (region is null
                ? candidates
                : candidates.Where(profile => string.Equals(profile.Region?.Trim(), region, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(profile => region is null && profile.Region is null)
            .ThenByDescending(profile => profile.VerifiedAt)
            .ThenBy(profile => profile.Id)
            .FirstOrDefault();

        if (target is null && candidates.Count > 0)
        {
            // A new rules-only profile would become the newest reference for this crop and hide the existing stages.
            var regions = string.Join(", ", candidates.Select(profile => profile.Region ?? "no region").Distinct());
            throw new ApiException(HttpStatusCode.Conflict, "REFERENCE_PROFILE_REGION_MISMATCH",
                $"This crop already has a verified reference for: {regions}. Leave Region empty or use one of these regions so the existing reference stays complete.");
        }

        // Admin research produces a new inactive draft. It must not mutate an active profile
        // or claim that an Agricultural Officer verified source values and field regime.
        var draft = new CropReferenceProfile
        {
            CropTypeId = crop.Id, VarietyName = varietyName, Region = target?.Region ?? region,
            SourceName = sourceName, SourceUrl = sourceUrl,
            SourceVersion = $"Officer review pending {now:yyyy-MM-dd}",
            VerifiedAt = null, IsActive = false,
            VerificationState = CropReferenceVerificationState.Draft, DraftVersion = 1,
            CreatedByUserId = adminId,
            Stages = target?.Stages.Where(stage => !stage.IsDeleted).Select(stage => new CropStageReference
            {
                StageName = stage.StageName, Sequence = stage.Sequence,
                TypicalMinDays = stage.TypicalMinDays, TypicalMaxDays = stage.TypicalMaxDays,
                Notes = stage.Notes, SourceName = stage.SourceName, SourceUrl = stage.SourceUrl,
                CreatedByUserId = adminId
            }).ToList() ?? [],
            Rules = target?.Rules.Where(rule => !rule.IsDeleted && !SameResource(rule)).Select(rule => new CropRuleReference
            {
                RuleType = rule.RuleType, RuleKey = rule.RuleKey, StructuredValueJson = rule.StructuredValueJson,
                SourceName = rule.SourceName, SourceUrl = rule.SourceUrl, VerifiedAt = default,
                CreatedByUserId = adminId
            }).ToList() ?? []
        };
        var newRule = new CropRuleReference
        {
            RuleType = CropResourceRequirementRule.RuleType,
            RuleKey = ruleKey,
            StructuredValueJson = structuredValueJson,
            SourceName = sourceName,
            SourceUrl = sourceUrl,
            VerifiedAt = default,
            CreatedByUserId = adminId
        };
        draft.Rules.Add(newRule);
        dbContext.CropReferenceProfiles.Add(draft);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin {AdminId} saved resource evidence draft {ProfileId} for officer verification", adminId, draft.Id);
        return new VerifiedResourceRequirementResponse(draft.Id, newRule.Id, ruleKey, crop.Id,
            varietyName, draft.Region, resource.Id, resource.Name, request.QuantityPerArea,
            resource.Unit, areaUnit, sourceName, sourceUrl, null, true, false,
            CropReferenceVerificationState.Draft);

        bool SameResource(CropRuleReference rule)
        {
            if (!rule.RuleType.Equals(CropResourceRequirementRule.RuleType, StringComparison.OrdinalIgnoreCase)
                || !CropResourceRequirementRule.TryParse(rule.StructuredValueJson, out var parsed, out _)) return false;
            return parsed!.ResourceId == resource.Id
                || (parsed.ResourceId is null && string.Equals(parsed.ResourceName, resource.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<CropType> LoadCropAsync(Guid cropTypeId, CancellationToken cancellationToken)
    {
        var crop = await dbContext.CropTypes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == cropTypeId && !item.IsDeleted, cancellationToken)
            ?? throw NotFound("Crop type");
        if (!crop.IsActive)
            throw new ApiException(HttpStatusCode.BadRequest, "CROP_INACTIVE", "The selected crop is inactive.");
        return crop;
    }

    private async Task<string?> LoadVarietyNameAsync(Guid cropTypeId, Guid? cropVarietyId, CancellationToken cancellationToken)
    {
        if (!cropVarietyId.HasValue) return null;
        return await dbContext.CropVarieties.AsNoTracking()
            .Where(item => item.Id == cropVarietyId.Value && item.CropTypeId == cropTypeId && item.IsActive && !item.IsDeleted)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(HttpStatusCode.BadRequest, "INVALID_CROP_VARIETY", "Selected variety is not active for this crop.");
    }

    private async Task<Resource> LoadResourceAsync(Guid resourceId, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == resourceId && !item.IsDeleted, cancellationToken)
            ?? throw NotFound("Resource");
        if (!resource.IsActive)
            throw new ApiException(HttpStatusCode.BadRequest, "RESOURCE_INACTIVE", "Inactive resources cannot be researched or given a requirement.");
        return resource;
    }

    private async Task<T> CallAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Resource requirement research timed out");
            throw new ApiException(HttpStatusCode.GatewayTimeout, "RESOURCE_RESEARCH_TIMEOUT",
                "The web research timed out. Nothing was saved.");
        }
        catch (CropFindingAIException exception)
        {
            var detail = exception.Detail;
            logger.LogWarning(
                "Resource requirement research received upstream status {StatusCode} request {RequestId} operation {Operation} stage {Stage} category {Category}",
                exception.ResponseStatusCode, detail?.RequestId, detail?.Operation, detail?.Stage, detail?.Category);
            var reference = detail?.RequestId is { Length: > 0 } requestId ? $" Reference: {requestId}." : string.Empty;
            if (exception.ResponseStatusCode == HttpStatusCode.GatewayTimeout
                || string.Equals(detail?.Category, "timeout", StringComparison.OrdinalIgnoreCase))
                throw new ApiException(HttpStatusCode.GatewayTimeout, "RESOURCE_RESEARCH_TIMEOUT",
                    $"The web research timed out. Nothing was saved.{reference}");
            if (exception.ResponseStatusCode == HttpStatusCode.ServiceUnavailable)
                throw new ApiException(HttpStatusCode.ServiceUnavailable, "RESOURCE_RESEARCH_CONFIGURATION_UNAVAILABLE",
                    $"The web research service is not configured. Nothing was saved.{reference}");
            throw new ApiException(HttpStatusCode.BadGateway, "RESOURCE_RESEARCH_FAILED",
                $"The web research could not be completed. Nothing was saved.{reference}");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Resource requirement research could not reach the AI service");
            throw new ApiException(HttpStatusCode.BadGateway, "RESOURCE_RESEARCH_UNAVAILABLE",
                "The web research service is unavailable. Nothing was saved.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or NotSupportedException)
        {
            logger.LogWarning(exception, "Resource requirement research failed");
            throw new ApiException(HttpStatusCode.BadGateway, "RESOURCE_RESEARCH_FAILED",
                "The web research could not be completed. Nothing was saved.");
        }
    }

    private Guid RequireAdmin()
    {
        if (!currentUser.IsInRole(ApplicationRole.Admin) || !currentUser.UserId.HasValue)
            throw new ApiException(HttpStatusCode.Forbidden, "ADMIN_REQUIRED", "An Admin account is required.");
        return currentUser.UserId.Value;
    }

    private static string RuleKeyFor(string resourceName)
    {
        var key = resourceName.Trim().ToLowerInvariant();
        return key.Length <= 160 ? key : key[..160];
    }

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
            throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors));
    }

    private static ApiException NotFound(string name) =>
        new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");
}
