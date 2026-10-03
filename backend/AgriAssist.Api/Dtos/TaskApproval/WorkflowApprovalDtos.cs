using System.Text.Json;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;

namespace AgriAssist.Api.Dtos.TaskApproval;

public sealed record ExistingFarmTaskSnapshot(Guid Id, Guid AssignedToUserId, DateTime DueAt, FarmTaskStatus Status);
public sealed record ExistingIrrigationSnapshot(Guid Id, Guid FieldId, DateTime ScheduledAt, int DurationMinutes, IrrigationScheduleStatus Status);
public sealed record SchedulingSource(string Kind, Guid Id, string Label, Guid? ProfileId = null,
    string? SourceVersion = null, DateTime? VerifiedAt = null, string? SourceUrl = null);
public sealed record SchedulingCandidateTask(Guid FarmId, string Title, string Description, DateTime DueAt, Guid AssignedToUserId,
    string? Reason = null, IReadOnlyList<SchedulingSource>? Sources = null);
public sealed record SchedulingCandidateIrrigation(Guid FieldId, DateTime ScheduledAt, int DurationMinutes, string Notes,
    string? Reason = null, IReadOnlyList<SchedulingSource>? Sources = null);
public sealed record SchedulingCandidateReservation(Guid InventoryStockId, decimal Quantity, string Purpose, decimal? EstimatedUnitCost,
    string? Reason = null, IReadOnlyList<SchedulingSource>? Sources = null);
public sealed record SchedulingConstraint(string Code, string Severity, string Message);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CropHealthGuidanceDecision>))]
public enum CropHealthGuidanceDecision { PendingDecision, Included, Rejected, NotApplicable }

public sealed record CropHealthCandidateTask(
    string ActionKey,
    CropHealthActionType ActionType,
    string TaskCategory,
    string Title,
    string Description,
    string TimingCategory,
    DateTime DueAt,
    Guid AssignedToUserId,
    bool Included,
    string? SchedulingNote,
    Guid InspectionId,
    Guid InspectionImageId,
    Guid AnalysisId,
    Guid ReviewId);

public sealed record CropHealthGuidanceCandidate(
    string CropHealthObservation,
    string PossibleConcern,
    string UncertaintyGuidance,
    IReadOnlyList<string> PrePlantingActions,
    IReadOnlyList<string> MonitoringActions,
    string? EscalationGuidance,
    string WhyThisIsRecommended,
    CropHealthGuidanceDecision Decision,
    Guid? DecidedByUserId = null,
    DateTime? DecidedAt = null,
    string? RejectionReason = null);
public sealed record SchedulingStageEvidence(Guid Id, string StageName, int Sequence, int? TypicalMinDays, int? TypicalMaxDays, string SourceName, string? SourceUrl);
public sealed record SchedulingIrrigationRuleEvidence(Guid Id, string RuleKey, int DayOffsetFromPlanting, string StartTimeUtc, int DurationMinutes, string SourceName, string? SourceUrl, DateTime VerifiedAt);
public sealed record SchedulingEvidenceBundle(
    Guid? ProfileId,
    string? SourceName,
    string? SourceUrl,
    string? SourceVersion,
    DateTime? VerifiedAt,
    Guid? CoordinatorStepId,
    Guid? FieldAnalysisStepId,
    Guid? WeatherResourceStepId,
    IReadOnlyList<SchedulingStageEvidence> Stages,
    IReadOnlyList<SchedulingIrrigationRuleEvidence> IrrigationRules,
    IReadOnlyList<Guid> InvalidIrrigationRuleIds);

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
    IReadOnlyList<ExistingIrrigationSnapshot> ExistingIrrigation,
    SchedulingEvidenceBundle? Evidence = null,
    int ContractVersion = 2);

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
    IReadOnlyList<SchedulingConstraint> Constraints,
    int ContractVersion = 1,
    IReadOnlyList<CropHealthCandidateTask>? CropHealthCandidateTasks = null,
    CropHealthGuidanceCandidate? CropHealthGuidance = null);

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

public sealed record CropHealthGuidanceDecisionRequest(
    int CandidateRevision,
    int ExpectedWorkflowVersion,
    CropHealthGuidanceDecision Decision,
    string IdempotencyKey,
    string? RejectionReason);

public sealed record CropHealthCandidateOperationalRequest(
    int CandidateRevision,
    int ExpectedWorkflowVersion,
    bool Included,
    DateTime DueAt,
    Guid AssignedToUserId,
    string? SchedulingNote);

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
