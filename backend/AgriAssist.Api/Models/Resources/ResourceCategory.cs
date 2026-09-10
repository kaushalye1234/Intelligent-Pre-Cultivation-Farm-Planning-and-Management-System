using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Resources;

public sealed class ResourceCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
