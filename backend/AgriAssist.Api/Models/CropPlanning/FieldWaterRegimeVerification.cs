using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public enum CropReferenceVerificationState
{
    Draft = 1,
    Verified = 2,
    LegacyReviewRequired = 3
}

public enum WaterRegime
{
    Irrigated = 1,
    Rainfed = 2
}

public sealed class FieldWaterRegimeVerification : AuditableEntity
{
    public Guid FieldId { get; set; }
    public Field? Field { get; set; }
    public WaterRegime WaterRegime { get; set; }
    public string Observation { get; set; } = string.Empty;
    public Guid VerifiedByUserId { get; set; }
    public DateTime VerifiedAt { get; set; }
}
