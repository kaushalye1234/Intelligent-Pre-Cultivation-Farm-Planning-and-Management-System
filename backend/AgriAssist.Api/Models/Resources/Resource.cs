using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Resources;

public sealed class Resource : AuditableEntity
{
    public Guid ResourceCategoryId { get; set; }
    public ResourceCategory? ResourceCategory { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
