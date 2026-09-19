namespace AgriAssist.Api.Services.Shared;

public interface IPasswordPolicyService
{
    ValueTask<PasswordPolicyViolation?> ValidateAsync(
        string password,
        string email,
        string fullName,
        CancellationToken cancellationToken);
}

public sealed record PasswordPolicyViolation(string Code, string Message);
