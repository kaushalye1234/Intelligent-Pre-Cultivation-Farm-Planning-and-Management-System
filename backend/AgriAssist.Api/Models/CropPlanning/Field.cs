using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class Field : AuditableEntity
{
    public Guid FarmId { get; set; }
    public Farm? Farm { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Area { get; set; }
    public string SoilType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
