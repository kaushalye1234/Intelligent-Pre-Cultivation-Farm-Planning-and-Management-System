using AgriAssist.Api.Models.Inspections;
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
    DateOnly PreferredStartDate,
    Guid? CropVarietyId = null,
    string? CropVarietyName = null,
    string CultivationSeason = "NotSure",
    DateOnly PreferredEndDate = default,
    Guid? PreviousCropTypeId = null,
    string? PreviousCropTypeName = null,
    IReadOnlyList<string>? PreviousKnownProblems = null);

public sealed record CropPlanningCoordinatorOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string? ReferenceDataStatus,
    string? ObjectiveSummary,
    IReadOnlyList<CropPlanningDelegatedStepResponse>? Steps);

public sealed record FieldAnalysisInput(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid PrePlantingInspectionId,
    Guid FieldId,
    Guid? CropCycleId,
    IReadOnlyList<string> RequestedAnalysis,
    Guid? CropReferenceProfileId,
    Guid? AgentStepId);

public sealed record PrePlantingAssessmentRequest(
    string SoilCondition,
    string WaterAvailability,
    string IrrigationAvailability,
    string DrainageCondition,
    string GeneralFieldCondition,
    string PlantingReadiness,
    string RisksAndConcerns,
    string OfficerNotes);

public sealed record PrePlantingAssessmentImageResponse(
    Guid Id,
    string Url,
    string ContentType,
    long SizeBytes);

public sealed record PrePlantingAssessmentResponse(
    Guid InspectionId,
    Guid CropPlanRequestId,
    Guid FieldId,
    InspectionStatus Status,
    DateTime ScheduledAt,
    DateTime? CompletedAt,
    string SoilCondition,
    string WaterAvailability,
    string IrrigationAvailability,
    string DrainageCondition,
    string GeneralFieldCondition,
    string PlantingReadiness,
    string RisksAndConcerns,
    string OfficerNotes,
    IReadOnlyList<PrePlantingAssessmentImageResponse> Images);

public sealed record FieldAnalysisFieldConditionResponse(
    string Summary,
    IReadOnlyList<Guid> EvidenceInspectionIds);

public sealed record FieldAnalysisOpenIssueResponse(
    Guid IssueId,
    string Severity,
    string Status,
    Guid? EvidenceInspectionId);

public sealed record FieldAnalysisOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    FieldAnalysisFieldConditionResponse FieldCondition,
    IReadOnlyList<FieldAnalysisOpenIssueResponse> OpenIssues,
    string Priority);

public sealed record FieldAnalysisRunResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid FieldAnalysisStepId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings);

public sealed record Member3HandoffResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid? FieldId,
    Guid? CropCycleId,
    string FieldLocationContext,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    string FieldAnalysisSummary,
    string Priority,
    IReadOnlyList<Guid> EvidenceInspectionIds,
    IReadOnlyList<FieldAnalysisOpenIssueResponse> OpenIssues,
    IReadOnlyList<string> Warnings);
