using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class CropStageReference : AuditableEntity
{
    public Guid CropReferenceProfileId { get; set; }
    public CropReferenceProfile? CropReferenceProfile { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int? TypicalMinDays { get; set; }
    public int? TypicalMaxDays { get; set; }
    public string? Notes { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
}
