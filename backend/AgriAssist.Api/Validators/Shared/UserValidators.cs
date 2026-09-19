using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Validators.Shared;

public sealed class CreateStaffUserRequestValidator : IRequestValidator<CreateStaffUserRequest>
{
    public IReadOnlyList<string> Validate(CreateStaffUserRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 120)
        {
            errors.Add("Full name is required and must be 120 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Trim().Length > 180
            || !request.Email.Contains('@'))
        {
            errors.Add("A valid email address is required.");
        }

        if (request.Role is < ApplicationRole.FieldOfficer or > ApplicationRole.Admin)
        {
            errors.Add("Staff role must be Field Officer, Resource Officer, Agricultural Officer, or Admin.");
        }

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            errors.Add("Temporary password is required.");
        }

        return errors;
    }
}

public sealed class ResetStaffPasswordRequestValidator : IRequestValidator<ResetStaffPasswordRequest>
{
    public IReadOnlyList<string> Validate(ResetStaffPasswordRequest request)
    {
        return string.IsNullOrWhiteSpace(request.TemporaryPassword)
            ? ["Temporary password is required."]
            : [];
    }
}
