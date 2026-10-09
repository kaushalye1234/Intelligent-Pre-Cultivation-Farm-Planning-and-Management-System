using AgriAssist.Api.Services.Shared;

namespace AgriAssist.Api.Bootstrap;

public enum AdminBootstrapStartupResult
{
    Disabled,
    AdminAlreadyExists,
    Created
}

/// <summary>
/// Supports first-deploy Admin provisioning without exposing a public setup endpoint.
/// Disable and remove its environment settings after the first successful deployment.
/// </summary>
public sealed class AdminBootstrapOnStartupService(
    IConfiguration configuration,
    IAdminBootstrapService adminBootstrapService)
{
    public async Task<AdminBootstrapStartupResult> RunIfEnabledAsync(
        CancellationToken cancellationToken)
    {
        var enabledValue = configuration["AdminBootstrap:Enabled"];
        if (string.IsNullOrWhiteSpace(enabledValue))
        {
            return AdminBootstrapStartupResult.Disabled;
        }

        if (!bool.TryParse(enabledValue, out var enabled))
        {
            throw new InvalidOperationException("AdminBootstrap:Enabled must be true or false.");
        }

        if (!enabled)
        {
            return AdminBootstrapStartupResult.Disabled;
        }

        if (await adminBootstrapService.AnyAdminExistsAsync(cancellationToken))
        {
            return AdminBootstrapStartupResult.AdminAlreadyExists;
        }

        var fullName = RequireSetting("AdminBootstrap:FullName");
        var email = RequireSetting("AdminBootstrap:Email");
        var password = RequireSetting("AdminBootstrap:Password");

        try
        {
            await adminBootstrapService.BootstrapAsync(fullName, email, password, cancellationToken);
            return AdminBootstrapStartupResult.Created;
        }
        catch (ApiException exception) when (exception.Code == "ADMIN_ALREADY_EXISTS")
        {
            // Another API instance may have completed first-admin provisioning while
            // this instance was starting. The persisted Admin is the source of truth.
            return AdminBootstrapStartupResult.AdminAlreadyExists;
        }
    }

    private string RequireSetting(string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required when AdminBootstrap:Enabled is true.");
        }

        return value;
    }
}
