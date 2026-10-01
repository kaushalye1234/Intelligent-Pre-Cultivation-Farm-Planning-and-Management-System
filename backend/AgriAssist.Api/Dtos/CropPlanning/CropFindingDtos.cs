using System.Text.Json;

namespace AgriAssist.Api.Dtos.CropPlanning;

public sealed record SuggestCropsRequest(string? Context = null, int MaxSuggestions = 8);

public sealed record SuggestVarietiesRequest(
    Guid CropTypeId,
    string? Context = null,
    int MaxSuggestions = 8);

public sealed record DiscoverReferencesRequest(
    Guid CropTypeId,
    Guid? CropVarietyId = null,
    string? Region = null);

public sealed record SuggestCropsInput(
    Guid AdminUserId,
    string? Context,
    int MaxSuggestions);

public sealed record SuggestVarietiesInput(
    Guid AdminUserId,
    Guid CropTypeId,
    string CropName,
    string? Context,
    int MaxSuggestions);

public sealed record DiscoverReferencesInput(
    Guid AdminUserId,
    Guid CropTypeId,
    string CropName,
    Guid? CropVarietyId,
    string? VarietyName,
    string? Region);

public sealed record EvidenceProvenance(
    string SourceId,
    string SourceName,
    string OrganizationName,
    string OriginalUrl,
    string FinalUrl,
    string SourceCategory,
    string Country,
    string SourceClassification,
    int Stage,
    string EvidenceText,
    int? PageNumber,
    string? Section);

public sealed record DiscoveredSource(
    string SourceId,
    string Title,
    string OrganizationName,
    string OriginalUrl,
    string FinalUrl,
    string SourceCategory,
    string Country,
    string SourceClassification,
    int Stage,
    string? ContentType,
    string RetrievalStatus,
    string? RetrievedAt,
    string AcceptanceReason,
    string? RejectionReason,
    bool ManualReviewRequired,
    int? PageCount,
    IReadOnlyList<string> Warnings,
    bool ExistingReference = false,
    Guid? ExistingReferenceId = null);

public sealed record CropSuggestion(
    string Id,
    string Name,
    string? Description,
    string EvidenceStatus,
    string Explanation,
    IReadOnlyList<EvidenceProvenance> Provenance,
    IReadOnlyList<string> Warnings,
    bool AlreadyExists = false,
    Guid? ExistingCropTypeId = null);

public sealed record VarietySuggestion(
    string Id,
    string Name,
    string? Description,
    string EvidenceStatus,
    string Explanation,
    IReadOnlyList<EvidenceProvenance> Provenance,
    IReadOnlyList<string> Warnings,
    bool AlreadyExists = false,
    Guid? ExistingCropVarietyId = null);

public sealed record ReferenceDraftItem(
    string Id,
    string Field,
    JsonElement SuggestedValue,
    string DisplayValue,
    string EvidenceStatus,
    string Explanation,
    IReadOnlyList<EvidenceProvenance> Provenance,
    IReadOnlyList<string> Warnings,
    string? ConflictGroupId = null);

public sealed record ReferenceSourceDraft(
    DiscoveredSource Source,
    IReadOnlyList<ReferenceDraftItem> Items,
    IReadOnlyList<string> Analysis,
    IReadOnlyList<string> Recommendations);

public sealed record CropSuggestionsResponse(
    string RequestId,
    string Action,
    bool UsedInternationalFallback,
    IReadOnlyList<DiscoveredSource> Sources,
    IReadOnlyList<CropSuggestion> Suggestions,
    IReadOnlyList<string> Analysis,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> Warnings);

public sealed record VarietySuggestionsResponse(
    string RequestId,
    string Action,
    Guid CropTypeId,
    string CropName,
    bool UsedInternationalFallback,
    IReadOnlyList<DiscoveredSource> Sources,
    IReadOnlyList<VarietySuggestion> Suggestions,
    IReadOnlyList<string> Analysis,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> Warnings);

public sealed record ReferenceDiscoveryResponse(
    string RequestId,
    string Action,
    Guid CropTypeId,
    string CropName,
    Guid? CropVarietyId,
    string? VarietyName,
    bool UsedInternationalFallback,
    IReadOnlyList<ReferenceSourceDraft> SourceDrafts,
    IReadOnlyList<string> Analysis,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> UnsupportedFields,
    IReadOnlyList<string> Warnings);
