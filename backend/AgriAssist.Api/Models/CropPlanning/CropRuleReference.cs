using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class CropRuleReference : AuditableEntity
{
    public Guid CropReferenceProfileId { get; set; }
    public CropReferenceProfile? CropReferenceProfile { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string RuleKey { get; set; } = string.Empty;
    public string StructuredValueJson { get; set; } = "{}";
    public string SourceName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public DateTime VerifiedAt { get; set; }
}
