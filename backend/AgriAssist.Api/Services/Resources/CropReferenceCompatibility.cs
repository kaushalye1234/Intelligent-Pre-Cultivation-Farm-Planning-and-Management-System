using AgriAssist.Api.Models.CropPlanning;

namespace AgriAssist.Api.Services.Resources;

public static class CropReferenceCompatibility
{
    public static bool IsVerifiedForPlan(CropReferenceProfile profile, CropPlanRequest plan, int? version = null) =>
        !profile.IsDeleted && profile.IsActive
        && profile.VerificationState == CropReferenceVerificationState.Verified
        && profile.VerifiedAt is { } verifiedAt && verifiedAt <= DateTime.UtcNow
        && profile.VerifiedByUserId.HasValue
        && (!version.HasValue || profile.DraftVersion == version)
        && profile.CropTypeId == plan.CropTypeId
        && (profile.VarietyName is null || string.Equals(profile.VarietyName, plan.CropVariety?.Name, StringComparison.OrdinalIgnoreCase))
        && CropReferenceRegionMatcher.Rank(profile.Region, plan.Farm) >= 0
        && plan.FieldId.HasValue && plan.Field is { IsActive: true, IsDeleted: false }
        && profile.WaterRegime.HasValue
        && profile.FieldWaterRegimeVerification is { IsDeleted: false } regime
        && regime.FieldId == plan.FieldId && regime.WaterRegime == profile.WaterRegime
        && regime.VerifiedByUserId == profile.VerifiedByUserId && regime.VerifiedAt <= DateTime.UtcNow;
}
