using AgriAssist.Api.Models.TaskApproval;

namespace AgriAssist.Api.Dtos.TaskApproval;

public sealed record FarmTaskRequest(Guid FarmId, string Title, string Description, DateTime DueAt, Guid AssignedToUserId, FarmTaskStatus Status);
public sealed record FarmTaskResponse(Guid Id, Guid FarmId, string Title, string Description, DateTime DueAt, Guid AssignedToUserId, FarmTaskStatus Status);

public sealed record IrrigationScheduleRequest(Guid FieldId, DateTime ScheduledAt, int DurationMinutes, string Notes, IrrigationScheduleStatus Status);
public sealed record IrrigationScheduleResponse(Guid Id, Guid FieldId, DateTime ScheduledAt, int DurationMinutes, string Notes, IrrigationScheduleStatus Status);

public sealed record ApprovalActionRequest(string Comment, Guid? AgentWorkflowId);
public sealed record ApprovalDecisionResponse(Guid Id, Guid? FarmTaskId, Guid? IrrigationScheduleId, Guid DecidedByUserId, Guid? AgentWorkflowId, ApprovalDecisionType Decision, string Comment, DateTime CreatedAt);
