using System.Text;

namespace AgriAssist.Api.Services.Shared;

public sealed class PasswordPolicyService(
    ICompromisedPasswordChecker compromisedPasswordChecker) : IPasswordPolicyService
{
    private const int MinimumLength = 12;
    public const int BcryptMaximumInputBytes = 72;

    public async ValueTask<PasswordPolicyViolation?> ValidateAsync(
        string password,
        string email,
        string fullName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            return new PasswordPolicyViolation(
                "PASSWORD_TOO_WEAK",
                "Password must be at least 12 characters.");
        }

        if (Encoding.UTF8.GetByteCount(password) > BcryptMaximumInputBytes)
        {
            return new PasswordPolicyViolation(
                "PASSWORD_EXCEEDS_HASH_LIMIT",
                "Password exceeds the supported BCrypt input size.");
        }

        var candidate = NormalizeIdentifier(password);
        if (candidate == NormalizeIdentifier(email) || candidate == NormalizeIdentifier(fullName))
        {
            return new PasswordPolicyViolation(
                "PASSWORD_IDENTIFIER_MATCH",
                "Password must not equal the email address or full name.");
        }

        if (await compromisedPasswordChecker.IsCompromisedAsync(password, cancellationToken))
        {
            return new PasswordPolicyViolation(
                "PASSWORD_COMPROMISED",
                "Choose a password that is not commonly used or compromised.");
        }

        return null;
    }

    private static string NormalizeIdentifier(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
}
