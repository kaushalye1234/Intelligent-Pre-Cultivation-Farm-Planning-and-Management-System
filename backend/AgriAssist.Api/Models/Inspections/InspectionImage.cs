using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Inspections;

public sealed class InspectionImage : AuditableEntity
{
    public Guid FieldInspectionId { get; set; }
    public FieldInspection? FieldInspection { get; set; }
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public string? AssetId { get; set; }
    public long? StorageVersion { get; set; }
    public string DeliveryType { get; set; } = "upload";
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? ContentSha256 { get; set; }
    public bool IsRepresentativeForAi { get; set; }
}
