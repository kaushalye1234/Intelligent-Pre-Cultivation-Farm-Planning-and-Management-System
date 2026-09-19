using System.ComponentModel.DataAnnotations;

namespace AgriAssist.Api.Configuration;

public static class RateLimitPolicyNames
{
    public const string Login = "login";
    public const string FarmerRegistration = "farmer-registration";
    public const string TemporaryPasswordChange = "temporary-password-change";
    public const string AdminReauthentication = "admin-reauthentication";
    public const string AdminUserManagement = "admin-user-management";
}

public static class AuthenticationClaimNames
{
    public const string TokenVersion = "token_version";
    public const string TokenUse = "token_use";
}

public static class AuthenticationTokenUses
{
    public const string Access = "access";
    public const string PasswordChange = "password_change";
}

public static class AuthorizationPolicyNames
{
    public const string PasswordChange = "password-change";
}

public static class AuthenticationFailureItems
{
    public const string ErrorCode = "authentication-error-code";
}

public sealed class SecurityRateLimitOptions
{
    public const string SectionName = "Security:RateLimits";

    public RateLimitRuleOptions Login { get; set; } = new() { PermitLimit = 5, WindowSeconds = 900 };
    public RateLimitRuleOptions FarmerRegistration { get; set; } = new() { PermitLimit = 3, WindowSeconds = 3600 };
    public RateLimitRuleOptions TemporaryPasswordChange { get; set; } = new() { PermitLimit = 5, WindowSeconds = 900 };
    public RateLimitRuleOptions AdminReauthentication { get; set; } = new() { PermitLimit = 5, WindowSeconds = 900 };
    public RateLimitRuleOptions AdminUserManagement { get; set; } = new() { PermitLimit = 10, WindowSeconds = 600 };
}

public sealed class RateLimitRuleOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int WindowSeconds { get; set; }
}

public sealed class PasswordSecurityOptions
{
    public const string SectionName = "Security:Password";

    public string[] CompromisedPasswords { get; set; } = [];
}
