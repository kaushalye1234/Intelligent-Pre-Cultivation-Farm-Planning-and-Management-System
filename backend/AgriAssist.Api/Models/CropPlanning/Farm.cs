using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.CropPlanning;

public sealed class Farm : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal TotalArea { get; set; }
    public Guid OwnerUserId { get; set; }
    public AppUser? OwnerUser { get; set; }
    public List<Field> Fields { get; set; } = [];
}
