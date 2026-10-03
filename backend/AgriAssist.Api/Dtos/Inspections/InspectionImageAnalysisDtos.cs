using System.Text.Json.Serialization;

namespace AgriAssist.Api.Dtos.Inspections;

public static class InspectionImageAnalysisContract
{
    public const int Version = 1;
}

[JsonConverter(typeof(JsonStringEnumConverter<CropHealthIssueCategory>))]
public enum CropHealthIssueCategory { Pest, Fungal, Bacterial, DiseaseLike, NutrientStress, EnvironmentalStress, PhysicalDamage, Other, Unknown }

[JsonConverter(typeof(JsonStringEnumConverter<CropHealthActionType>))]
public enum CropHealthActionType { FieldSanitation, RemoveAffectedResidue, SeparateAffectedMaterial, InspectNearbyPlants, MonitorSymptoms, PrePlantingCleanup, RequestFurtherAssessment }

public sealed record ImageAnalysisCapabilityResponse(
    int ContractVersion,
    int ImagePreprocessingVersion,
    int PromptContractVersion,
    int RelevanceRuleVersion,
    string SourcePolicyVersion,
    string SourcePolicyHash,
    string Provider,
    string Model);

public sealed record InspectionImageAnalysisAiInput(
    int ContractVersion,
    Guid AnalysisId,
    int ImagePreprocessingVersion,
    string CropName,
    string? VarietyName);

public sealed record ImageAnalysisSourceReference(
    string SourcePolicyId,
    string Organization,
    string Title,
    string Url,
    string SourceStage);

public sealed record InspectionImageAnalysisFinalResult(
    int ContractVersion,
    IReadOnlyList<string> VisibleFindings,
    CropHealthIssueCategory PossibleIssueCategory,
    IReadOnlyList<string> PossibleIssues,
    string Severity,
    string Uncertainty,
    IReadOnlyList<ImageAnalysisSourceReference> ValidatedSourceReferences,
    IReadOnlyList<CropHealthActionType> RecommendedNonChemicalActions,
    bool RequiresFurtherAssessment,
    string GroundingStatus);

public sealed record InspectionImageAnalysisAiResponse(
    int ContractVersion,
    string Status,
    object? Pass1Result,
    IReadOnlyList<object> EvidencePacket,
    InspectionImageAnalysisFinalResult? FinalResult,
    string? FailureCategory,
    string? FailureMessage);

[JsonConverter(typeof(JsonStringEnumConverter<ImageAnalysisReviewDecision>))]
public enum ImageAnalysisReviewDecision { Accepted, Edited, Rejected }

public sealed record InspectionImageAnalysisEditProjection(
    IReadOnlyList<string> VisibleFindings,
    IReadOnlyList<string> PossibleConcerns,
    string Severity,
    string Uncertainty,
    IReadOnlyList<CropHealthActionType> Actions,
    bool RequiresFurtherAssessment);

public sealed record InspectionImageAnalysisReviewRequest(
    ImageAnalysisReviewDecision Disposition,
    InspectionImageAnalysisEditProjection? EditedProjection,
    string? StaffNote);

public sealed record ReviewedCropHealthAction(
    CropHealthActionType ActionType,
    int Order,
    string Origin,
    IReadOnlyList<string> SourcePolicyIds);

public sealed record ReviewedImageAnalysisProjection(
    Guid AnalysisId,
    Guid InspectionImageId,
    IReadOnlyList<string> VisibleFindings,
    IReadOnlyList<string> PossibleConcerns,
    string Severity,
    string Uncertainty,
    IReadOnlyList<ReviewedCropHealthAction> Actions,
    IReadOnlyList<ImageAnalysisSourceReference> SourceReferences,
    bool RequiresFurtherAssessment,
    IReadOnlyList<string> OfficerEditedFields);

public sealed record InspectionImageAnalysisReviewResponse(
    Guid ReviewId,
    Guid AnalysisId,
    ImageAnalysisReviewDecision Disposition,
    ReviewedImageAnalysisProjection? Projection,
    IReadOnlyList<string> OfficerEditedFields,
    string? StaffNote,
    DateTime ReviewedAt);

public sealed record InspectionImageAnalysisStateResponse(
    Guid? AnalysisId,
    string Status,
    bool IsCurrent,
    bool IsReviewable,
    bool IsFrozen,
    InspectionImageAnalysisFinalResult? Result,
    InspectionImageAnalysisReviewResponse? EffectiveReview,
    string? FailureCategory,
    string? Message);

