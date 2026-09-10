using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Inspections;

public sealed class InspectionObservation : AuditableEntity
{
    public Guid FieldInspectionId { get; set; }
    public FieldInspection? FieldInspection { get; set; }
    public string ObservationType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
