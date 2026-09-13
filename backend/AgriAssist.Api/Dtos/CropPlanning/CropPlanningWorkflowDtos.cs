using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Dtos.CropPlanning;

public sealed record CropPlanningWorkflowStartResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid CoordinatorStepId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings);

public sealed record CropPlanningWorkflowStatusResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    AgentWorkflowStatus Status,
    string CurrentStep,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<AgentStepStatusResponse> Steps,
    IReadOnlyList<string> Warnings);

public sealed record AgentStepStatusResponse(
    Guid Id,
    string AgentName,
    string StepName,
    int Sequence,
    AgentStepStatus Status,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? ErrorCode,
    string? ErrorMessageSafe);

public sealed record CropPlanningResultResponse(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string ReferenceDataStatus,
    string ObjectiveSummary,
    IReadOnlyList<CropPlanningDelegatedStepResponse> Steps);

public sealed record CropPlanningDelegatedStepResponse(int Sequence, string StepType, string AssignedAgent);

public sealed record CropPlanningCoordinatorInput(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid FarmerId,
    Guid FarmId,
    Guid? FieldId,
    Guid? CropCycleId,
    Guid CropTypeId,
    string Objective,
    decimal Budget,
    DateOnly PreferredStartDate);

public sealed record CropPlanningCoordinatorOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string? ReferenceDataStatus,
    string? ObjectiveSummary,
    IReadOnlyList<CropPlanningDelegatedStepResponse>? Steps);
