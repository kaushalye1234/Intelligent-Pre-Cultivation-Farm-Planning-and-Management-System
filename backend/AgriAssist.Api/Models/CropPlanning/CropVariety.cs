using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class CropVariety : AuditableEntity
{
    public Guid CropTypeId { get; set; }
    public CropType? CropType { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
