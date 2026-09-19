using AgriAssist.Api.Dtos.Shared;
namespace AgriAssist.Api.Validators.Shared;

public sealed class RegisterFarmerRequestValidator : IRequestValidator<RegisterFarmerRequest>
{
    public IReadOnlyList<string> Validate(RegisterFarmerRequest request)
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

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }

        return errors;
    }
}

public sealed class ChangeTemporaryPasswordRequestValidator : IRequestValidator<ChangeTemporaryPasswordRequest>
{
    public IReadOnlyList<string> Validate(ChangeTemporaryPasswordRequest request)
    {
        return string.IsNullOrWhiteSpace(request.NewPassword)
            ? ["New password is required."]
            : [];
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
