using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class CropReferenceProfile : AuditableEntity
{
    public Guid CropTypeId { get; set; }
    public CropType? CropType { get; set; }
    public string? VarietyName { get; set; }
    public string? Region { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string SourceVersion { get; set; } = string.Empty;
    public DateTime VerifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CropStageReference> Stages { get; set; } = [];
    public List<CropRuleReference> Rules { get; set; } = [];
}
