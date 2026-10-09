using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Resources;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.CropPlanning;

public sealed class CropReferenceVerificationService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ICropPlanningService cropPlanningService)
{
    public async Task<CropReferenceProfileDetailsResponse> VerifyAsync(
        Guid profileId, VerifyReferenceRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != ApplicationRole.AgriculturalOfficer || currentUser.UserId is not { } officerId)
            throw new ApiException(HttpStatusCode.Forbidden, "AGRICULTURAL_OFFICER_REQUIRED", "An Agricultural Officer must verify crop evidence.");
        var inputErrors = new VerifyReferenceRequestValidator().Validate(request);
        if (inputErrors.Count > 0)
            throw new ApiException(HttpStatusCode.BadRequest, "INVALID_VERIFICATION", string.Join(" ", inputErrors));

        var profile = await dbContext.CropReferenceProfiles
            .Include(item => item.Stages).Include(item => item.Rules)
            .SingleOrDefaultAsync(item => item.Id == profileId && !item.IsDeleted, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Crop reference profile was not found.");
        if (profile.VerificationState != CropReferenceVerificationState.Draft || profile.IsActive)
            throw new ApiException(HttpStatusCode.Conflict, "REFERENCE_NOT_DRAFT", "Only an inactive draft can be verified.");
        if (profile.DraftVersion != request.ExpectedDraftVersion)
            throw new ApiException(HttpStatusCode.Conflict, "REFERENCE_DRAFT_STALE", "The reference changed; reload it before verification.");
        var field = await dbContext.Fields.AsNoTracking().Include(item => item.Farm)
            .SingleOrDefaultAsync(item => item.Id == request.FieldId && item.IsActive && !item.IsDeleted, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.BadRequest, "FIELD_NOT_FOUND", "An active field is required.");
        if (field.Farm is null || field.Farm.IsDeleted || CropReferenceRegionMatcher.Rank(profile.Region, field.Farm) < 0)
            throw new ApiException(HttpStatusCode.BadRequest, "REFERENCE_REGION_MISMATCH", "The reference region does not match the observed field.");

        if (profile.Stages.Count == 0 || profile.Rules.Count == 0
            || !Cited(profile.SourceName, profile.SourceUrl)
            || profile.Stages.Any(item => item.IsDeleted || !Cited(item.SourceName, item.SourceUrl)
                || item.Sequence < 1 || !item.TypicalMinDays.HasValue || !item.TypicalMaxDays.HasValue
                || item.TypicalMinDays < 0 || item.TypicalMaxDays < 0
                || (item.TypicalMinDays.HasValue && item.TypicalMaxDays < item.TypicalMinDays))
            || profile.Stages.GroupBy(item => item.Sequence).Any(group => group.Count() > 1)
            || profile.Rules.Any(item => item.IsDeleted || !Cited(item.SourceName, item.SourceUrl)))
            throw new ApiException(HttpStatusCode.BadRequest, "REFERENCE_EVIDENCE_INCOMPLETE", "A cited source, ordered stages, and cited rules are required.");

        var resourceRules = profile.Rules.Where(item => item.RuleType.Equals(CropResourceRequirementRule.RuleType, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (resourceRules.Length == 0)
            throw new ApiException(HttpStatusCode.BadRequest, "REFERENCE_RESOURCE_RULE_MISSING", "At least one applicable resource requirement is required.");
        foreach (var item in resourceRules)
        {
            if (!CropResourceRequirementRule.TryParse(item.StructuredValueJson, out var parsed, out _))
                throw new ApiException(HttpStatusCode.BadRequest, "REFERENCE_RESOURCE_RULE_INVALID", "A resource rule has invalid quantity or units.");
            var resourceQuery = dbContext.Resources.AsNoTracking().Where(resource => resource.IsActive && !resource.IsDeleted);
            var matches = parsed!.ResourceId is { } resourceId
                ? await resourceQuery.Where(resource => resource.Id == resourceId).ToListAsync(cancellationToken)
                : await resourceQuery.Where(resource => resource.Name.ToLower() == parsed.ResourceName.ToLower()).Take(2).ToListAsync(cancellationToken);
            var resource = matches.Count == 1 ? matches[0] : null;
            if (resource is null || !string.Equals(resource.Unit.Trim(), parsed.ResourceUnit.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ApiException(HttpStatusCode.BadRequest, "REFERENCE_RESOURCE_MISMATCH", "A required inventory resource or its unit does not match the cited rule.");
        }

        var now = DateTime.UtcNow;
        var regime = new FieldWaterRegimeVerification
        {
            FieldId = field.Id, WaterRegime = request.WaterRegime,
            Observation = request.Observation.Trim(), VerifiedByUserId = officerId,
            VerifiedAt = now, CreatedByUserId = officerId
        };
        dbContext.FieldWaterRegimeVerifications.Add(regime);
        profile.FieldWaterRegimeVerificationId = regime.Id;
        profile.WaterRegime = request.WaterRegime;
        profile.VerificationState = CropReferenceVerificationState.Verified;
        profile.VerifiedByUserId = officerId;
        profile.VerifiedAt = now;
        profile.IsActive = true;
        profile.DraftVersion++;
        profile.UpdatedAt = now;
        profile.UpdatedByUserId = officerId;
        foreach (var rule in profile.Rules) { rule.VerifiedAt = now; rule.UpdatedAt = now; rule.UpdatedByUserId = officerId; }
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(HttpStatusCode.Conflict, "REFERENCE_DRAFT_STALE", "The reference changed; reload it before verification.");
        }
        return await cropPlanningService.GetReferenceProfileAsync(profileId, cancellationToken);
    }

    private static bool Cited(string? name, string? url) =>
        !string.IsNullOrWhiteSpace(name) && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http";
}
