using AgriAssist.Api.Dtos.CropPlanning;

namespace AgriAssist.Api.Dtos.Resources;

/// <summary>Admin request: research the requirement of one inventory resource for one crop. Never saves anything.</summary>
public sealed record ResourceRequirementResearchRequest(
    Guid CropTypeId,
    Guid ResourceId,
    Guid? CropVarietyId = null,
    string? Region = null);

/// <summary>Body sent to the ai-service. Actor identity, crop/resource names and units come from the backend.</summary>
public sealed record ResourceRequirementResearchInput(
    Guid ActorUserId,
    Guid CropTypeId,
    string CropName,
    Guid? CropVarietyId,
    string? VarietyName,
    string? Region,
    Guid ResourceId,
    string ResourceName,
    string ResourceUnit,
    IReadOnlyList<string> OtherCropNames);

public sealed record ResourceRequirementComponent(string Label, decimal Quantity, string EvidenceText);

public sealed record ResourceRequirementRecommendation(
    string Id,
    decimal QuantityPerArea,
    string ResourceUnit,
    string AreaUnit,
    bool UnitMatchesInventory,
    string Basis,
    IReadOnlyList<ResourceRequirementComponent> Components,
    string EvidenceStatus,
    string CropContext,
    EvidenceProvenance Source,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Draft research result. Status is PendingVerification, ConflictingSources, NoVerifiedRecommendationFound or
/// EvidenceValidationFailed. Verified is always false: only the verify endpoint can store a rule.
/// </summary>
public sealed record ResourceRequirementResearchResponse(
    string RequestId,
    string Status,
    bool Verified,
    Guid CropTypeId,
    string CropName,
    Guid? CropVarietyId,
    string? VarietyName,
    string? Region,
    Guid ResourceId,
    string ResourceName,
    string ResourceUnit,
    decimal? SuggestedQuantityPerArea,
    string? SuggestedResourceUnit,
    string? SuggestedAreaUnit,
    string? SourceName,
    string? SourceUrl,
    string? Evidence,
    bool UsedInternationalFallback,
    IReadOnlyList<ResourceRequirementRecommendation> Recommendations,
    IReadOnlyList<string> RejectedClaims,
    IReadOnlyList<DiscoveredSource> Sources,
    IReadOnlyList<string> Warnings);

/// <summary>Admin "Verify &amp; Save": the values the Admin checked against the cited source.</summary>
public sealed record VerifyResourceRequirementRequest(
    Guid CropTypeId,
    Guid ResourceId,
    decimal QuantityPerArea,
    string ResourceUnit,
    string AreaUnit,
    string SourceName,
    string SourceUrl,
    string Evidence,
    Guid? CropVarietyId = null,
    string? Region = null,
    string? ResearchRequestId = null);

public sealed record VerifiedResourceRequirementResponse(
    Guid CropReferenceProfileId,
    Guid RuleId,
    string RuleKey,
    Guid CropTypeId,
    string? VarietyName,
    string? Region,
    Guid ResourceId,
    string ResourceName,
    decimal QuantityPerArea,
    string ResourceUnit,
    string AreaUnit,
    string SourceName,
    string SourceUrl,
    DateTime VerifiedAt,
    bool CreatedReferenceProfile,
    bool ReplacedPreviousRule);
