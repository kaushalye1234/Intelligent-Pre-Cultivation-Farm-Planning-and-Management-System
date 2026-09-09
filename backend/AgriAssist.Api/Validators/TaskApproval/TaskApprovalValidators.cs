using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Validators.Shared;

namespace AgriAssist.Api.Validators.TaskApproval;

public sealed class FarmTaskRequestValidator : IRequestValidator<FarmTaskRequest>
{
    public IReadOnlyList<string> Validate(FarmTaskRequest request)
    {
        var errors = new List<string>();
        if (request.FarmId == Guid.Empty) errors.Add("Farm is required.");
        if (request.AssignedToUserId == Guid.Empty) errors.Add("Assigned user is required.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 160) errors.Add("Task title is required and must be 160 characters or fewer.");
        if (request.Description.Length > 1500) errors.Add("Task description must be 1500 characters or fewer.");
        return errors;
    }
}

public sealed class IrrigationScheduleRequestValidator : IRequestValidator<IrrigationScheduleRequest>
{
    public IReadOnlyList<string> Validate(IrrigationScheduleRequest request)
    {
        var errors = new List<string>();
        if (request.FieldId == Guid.Empty) errors.Add("Field is required.");
        if (request.DurationMinutes <= 0) errors.Add("Duration must be positive.");
        if (request.Notes.Length > 1000) errors.Add("Notes must be 1000 characters or fewer.");
        return errors;
    }
}

public sealed class ApprovalActionRequestValidator : IRequestValidator<ApprovalActionRequest>
{
    public IReadOnlyList<string> Validate(ApprovalActionRequest request)
    {
        var errors = new List<string>();
        if (request.Comment.Length > 1000) errors.Add("Approval comment must be 1000 characters or fewer.");
        return errors;
    }
}
