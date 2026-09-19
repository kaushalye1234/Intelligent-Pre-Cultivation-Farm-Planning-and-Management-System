using AgriAssist.Api.Configuration;
using Microsoft.Extensions.Options;

namespace AgriAssist.Api.Services.Shared;

public sealed class ConfiguredCompromisedPasswordChecker(
    IOptions<PasswordSecurityOptions> options) : ICompromisedPasswordChecker
{
    public ValueTask<bool> IsCompromisedAsync(string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isCompromised = options.Value.CompromisedPasswords.Any(
            blocked => string.Equals(blocked.Trim(), password.Trim(), StringComparison.OrdinalIgnoreCase));

        return ValueTask.FromResult(isCompromised);
    }
}
