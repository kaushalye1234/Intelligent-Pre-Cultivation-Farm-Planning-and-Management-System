using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Validators.Shared;

public sealed class RegisterRequestValidator : IRequestValidator<RegisterRequest>
{
    public IReadOnlyList<string> Validate(RegisterRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 120)
        {
            errors.Add("Full name is required and must be 120 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 180 || !request.Email.Contains('@'))
        {
            errors.Add("A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 || request.Password.Length > 120)
        {
            errors.Add("Password must be between 8 and 120 characters.");
        }

        if (!Enum.IsDefined(typeof(ApplicationRole), request.Role))
        {
            errors.Add("Role is invalid.");
        }

        return errors;
    }
}

public sealed class LoginRequestValidator : IRequestValidator<LoginRequest>
{
    public IReadOnlyList<string> Validate(LoginRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            errors.Add("A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }

        return errors;
    }
}
