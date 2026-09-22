namespace AgriAssist.Api.Models.Shared;

public sealed class AppUser : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public ApplicationRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public int TokenVersion { get; set; } = 1;
    public DateTime? LastLoginAt { get; set; }
}
