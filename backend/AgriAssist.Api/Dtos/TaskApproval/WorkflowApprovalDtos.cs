using System.Text.Json;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;

namespace AgriAssist.Api.Dtos.TaskApproval;

public sealed record ExistingFarmTaskSnapshot(Guid Id, Guid AssignedToUserId, DateTime DueAt, FarmTaskStatus Status);
public sealed record ExistingIrrigationSnapshot(Guid Id, Guid FieldId, DateTime ScheduledAt, int DurationMinutes, IrrigationScheduleStatus Status);
public sealed record SchedulingCandidateTask(Guid FarmId, string Title, string Description, DateTime DueAt, Guid AssignedToUserId);
public sealed record SchedulingCandidateIrrigation(Guid FieldId, DateTime ScheduledAt, int DurationMinutes, string Notes);
public sealed record SchedulingCandidateReservation(Guid InventoryStockId, decimal Quantity, string Purpose, decimal? EstimatedUnitCost);
public sealed record SchedulingConstraint(string Code, string Severity, string Message);

public sealed record SchedulingValidationInput(
    Guid WorkflowId,
    int CandidateRevision,
    Guid CropPlanRequestId,
    Guid FarmId,
    Guid? FieldId,
    Guid AssignedToUserId,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    decimal Budget,
    JsonElement CoordinatorOutput,
    JsonElement FieldAnalysisOutput,
    JsonElement WeatherResourceOutput,
    IReadOnlyList<ExistingFarmTaskSnapshot> ExistingTasks,
    IReadOnlyList<ExistingIrrigationSnapshot> ExistingIrrigation);

public sealed record SchedulingValidationOutput(
    Guid WorkflowId,
    int CandidateRevision,
    string Status,
    bool RequiresHumanReview,
    bool RequiresHumanApproval,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<SchedulingCandidateTask> CandidateTasks,
    IReadOnlyList<SchedulingCandidateIrrigation> CandidateIrrigation,
    IReadOnlyList<SchedulingCandidateReservation> CandidateReservations,
    decimal? EstimatedCost,
    IReadOnlyList<SchedulingConstraint> Constraints);

public sealed class WorkflowApprovalQuery : PagedQuery
{
    public AgentWorkflowStatus? Status { get; set; }
}

public sealed record WorkflowSummaryResponse(
    Guid Id,
    Guid? CropPlanRequestId,
    string Objective,
    AgentWorkflowStatus Status,
    string CurrentStep,
    int CandidateRevision,
    int RevisionCount,
    int Version,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record WorkflowStepReviewResponse(
    Guid Id,
    string AgentName,
    string StepName,
    int Sequence,
    int CandidateRevision,
    AgentStepStatus Status,
    JsonElement Input,
    JsonElement Output,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorCode,
    string? ErrorMessageSafe);

public sealed record WorkflowValidationResponse(
    Guid Id,
    string ValidatorName,
    int CandidateRevision,
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    DateTime CreatedAt);

public sealed record WorkflowReviewResponse(
    WorkflowSummaryResponse Workflow,
    Guid FarmId,
    Guid? FieldId,
    decimal Budget,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    IReadOnlyList<WorkflowStepReviewResponse> Steps,
    IReadOnlyList<WorkflowValidationResponse> Validations,
    IReadOnlyList<ApprovalDecisionResponse> Decisions);

public sealed record WorkflowDecisionRequest(
    int CandidateRevision,
    int ExpectedWorkflowVersion,
    string IdempotencyKey,
    string Comment);

public sealed record WorkflowDecisionResponse(
    Guid WorkflowId,
    AgentWorkflowStatus Status,
    int CandidateRevision,
    int Version,
    ApprovalDecisionType Decision,
    Guid DecisionId,
    IReadOnlyList<Guid> FarmTaskIds,
    IReadOnlyList<Guid> IrrigationScheduleIds,
    IReadOnlyList<Guid> ReservationIds);

public sealed record WorkflowHistoryResponse(
    Guid WorkflowId,
    IReadOnlyList<WorkflowStepReviewResponse> Steps,
    IReadOnlyList<WorkflowValidationResponse> Validations,
    IReadOnlyList<ApprovalDecisionResponse> Decisions);
