using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.ExternalServices.Cloudinary;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Inspections;

public sealed class InspectionImageAnalysisService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ICloudinaryService cloudinaryService,
    IInspectionImageAnalysisAIClient aiClient,
    ILogger<InspectionImageAnalysisService> logger) : IInspectionImageAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ProhibitedTreatmentTerms =
        ["pesticide", "fungicide", "herbicide", "active ingredient", "dosage", "application rate", "spray interval", "spray schedule"];

    public async Task<ImageAnalysisCapabilityResponse?> TryGetCapabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var capability = await aiClient.GetImageAnalysisCapabilityAsync(cancellationToken);
            return capability.ContractVersion == InspectionImageAnalysisContract.Version
                && capability.SourcePolicyHash.Length == 64
                && !string.IsNullOrWhiteSpace(capability.Provider)
                && !string.IsNullOrWhiteSpace(capability.Model)
                ? capability
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogWarning("Inspection image-analysis capability is unavailable: {FailureCategory}", exception.GetType().Name);
            return null;
        }
    }

    public async Task<InspectionImageAnalysisStateResponse> AnalyzeAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        RequireFieldOfficer();
        var capability = await TryGetCapabilityAsync(cancellationToken);
        if (capability is null)
            return Unavailable("provider_unavailable", "AI image analysis is currently unavailable. Manual inspection remains available.");

        await VerifyAndInitializeRepresentativeMetadataAsync(cropPlanRequestId, cancellationToken);
        InspectionImageAnalysis analysis;
        CropPlanRequest planRequest;
        await using (var transaction = await BeginTransactionAsync(cancellationToken))
        {
            var inspection = await LockInspectionAsync(cropPlanRequestId, cancellationToken);
            RequireDraftOwner(inspection);
            planRequest = await LoadPlanRequestAsync(cropPlanRequestId, cancellationToken);
            var image = await CurrentRepresentativeAsync(inspection.Id, cancellationToken)
                ?? throw new ApiException(HttpStatusCode.Conflict, "REPRESENTATIVE_IMAGE_REQUIRED", "Select one representative inspection image before analysis.");
            EnsureFingerprintMetadata(image);
            var fingerprint = BuildFingerprint(image, planRequest, capability);

            var existing = await dbContext.InspectionImageAnalyses
                .Where(item => item.InspectionImageId == image.Id && item.AnalysisFingerprint == fingerprint
                    && (item.Status == InspectionImageAnalysisStatus.Running || item.Status == InspectionImageAnalysisStatus.Succeeded))
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null && existing.Status == InspectionImageAnalysisStatus.Running
                && existing.CreatedAt < DateTime.UtcNow.AddSeconds(-135))
            {
                existing.Status = InspectionImageAnalysisStatus.Interrupted;
                existing.FailureCategory = "abandoned_running_operation";
                existing.FailureMessageSafe = "A previous server operation ended before analysis could complete.";
                existing.CompletedAt = DateTime.UtcNow;
                existing.Version = checked(existing.Version + 1);
                await dbContext.SaveChangesAsync(cancellationToken);
                existing = null;
            }
            if (existing is not null)
            {
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return await MapStateAsync(existing, true, inspection.Status == InspectionStatus.Completed, cancellationToken);
            }

            analysis = new InspectionImageAnalysis
            {
                FieldInspectionId = inspection.Id,
                InspectionImageId = image.Id,
                AnalysisFingerprint = fingerprint,
                Status = InspectionImageAnalysisStatus.Running,
                CropTypeId = planRequest.CropTypeId,
                CropVarietyId = planRequest.CropVarietyId,
                InputSnapshotJson = JsonSerializer.Serialize(new
                {
                    inspectionId = inspection.Id,
                    inspectionImageId = image.Id,
                    image.PublicId,
                    image.AssetId,
                    image.StorageVersion,
                    image.ContentSha256,
                    planRequest.CropTypeId,
                    planRequest.CropVarietyId,
                    cropName = planRequest.CropType?.Name,
                    varietyName = planRequest.CropVariety?.Name
                }, JsonOptions),
                Provider = capability.Provider,
                Model = capability.Model,
                SourcePolicyVersion = capability.SourcePolicyVersion,
                SourcePolicyHash = capability.SourcePolicyHash,
                SchemaVersion = capability.ContractVersion,
                PromptContractVersion = capability.PromptContractVersion,
                ImagePreprocessingVersion = capability.ImagePreprocessingVersion,
                RelevanceRuleVersion = capability.RelevanceRuleVersion,
                CreatedByUserId = currentUser.UserId,
                UpdatedByUserId = currentUser.UserId
            };
            dbContext.InspectionImageAnalyses.Add(analysis);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                var concurrent = await dbContext.InspectionImageAnalyses.AsNoTracking()
                    .Where(item => item.InspectionImageId == image.Id && item.AnalysisFingerprint == fingerprint
                        && (item.Status == InspectionImageAnalysisStatus.Running || item.Status == InspectionImageAnalysisStatus.Succeeded))
                    .OrderByDescending(item => item.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (concurrent is not null)
                    return await MapStateAsync(concurrent, true, false, cancellationToken);
                throw;
            }
        }

        InspectionImageAnalysisAiResponse? response = null;
        string? failureCategory = null;
        try
        {
            using var operationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(125));
            response = await aiClient.RunImageAnalysisAsync(
                new InspectionImageAnalysisAiInput(
                    InspectionImageAnalysisContract.Version,
                    analysis.Id,
                    capability.ImagePreprocessingVersion,
                    planRequest.CropType?.Name ?? string.Empty,
                    planRequest.CropVariety?.Name),
                operationTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            failureCategory = "operation_timeout";
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            failureCategory = "ai_service_failure";
        }

        return await TerminalizeAsync(analysis.Id, capability, response, failureCategory, CancellationToken.None);
    }

    public async Task<InspectionImageAnalysisStateResponse> GetCurrentAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        var inspection = await VisibleInspectionAsync(cropPlanRequestId, cancellationToken);
        if (inspection.Status == InspectionStatus.Completed && inspection.FrozenImageAnalysisReviewId.HasValue)
        {
            var projection = await GetFrozenProjectionAsync(inspection, cancellationToken);
            if (projection is not null)
            {
                var analysis = await dbContext.InspectionImageAnalyses.AsNoTracking().SingleAsync(item => item.Id == projection.AnalysisId, cancellationToken);
                return await MapStateAsync(analysis, true, true, cancellationToken);
            }
        }
        if (inspection.Status == InspectionStatus.Completed)
            return Empty("NotIncluded", "No reviewed image analysis was frozen when this inspection was submitted.", true);

        var image = await CurrentRepresentativeAsync(inspection.Id, cancellationToken);
        if (image is null) return Empty("NoRepresentativeImage", "No representative image is selected.", inspection.Status == InspectionStatus.Completed);
        if (string.IsNullOrWhiteSpace(image.ContentSha256)) return Empty("NotAnalyzed", "The representative image has not been analyzed.", false);
        var capability = await TryGetCapabilityAsync(cancellationToken);
        if (capability is null) return Unavailable("provider_unavailable", "AI image analysis is currently unavailable.");
        var planRequest = await LoadPlanRequestAsync(cropPlanRequestId, cancellationToken);
        var fingerprint = BuildFingerprint(image, planRequest, capability);
        var analysisResult = await dbContext.InspectionImageAnalyses.AsNoTracking()
            .Where(item => item.InspectionImageId == image.Id && item.AnalysisFingerprint == fingerprint)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return analysisResult is null
            ? Empty("NotAnalyzed", "Analyze the current representative image to create a result.", false)
            : await MapStateAsync(analysisResult, true, false, cancellationToken);
    }

    public async Task<InspectionImageAnalysisReviewResponse> ReviewAsync(
        Guid cropPlanRequestId,
        InspectionImageAnalysisReviewRequest request,
        CancellationToken cancellationToken)
    {
        RequireFieldOfficer();
        ValidateReviewRequest(request);
        var capability = await TryGetCapabilityAsync(cancellationToken)
            ?? throw new ApiException(HttpStatusCode.ServiceUnavailable, "IMAGE_ANALYSIS_UNAVAILABLE", "Image analysis is currently unavailable.");
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var inspection = await LockInspectionAsync(cropPlanRequestId, cancellationToken);
        RequireDraftOwner(inspection);
        var planRequest = await LoadPlanRequestAsync(cropPlanRequestId, cancellationToken);
        var image = await CurrentRepresentativeAsync(inspection.Id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.Conflict, "REPRESENTATIVE_IMAGE_REQUIRED", "No representative image is selected.");
        EnsureFingerprintMetadata(image);
        var fingerprint = BuildFingerprint(image, planRequest, capability);
        var analysis = await dbContext.InspectionImageAnalyses
            .SingleOrDefaultAsync(item => item.InspectionImageId == image.Id
                && item.AnalysisFingerprint == fingerprint
                && item.Status == InspectionImageAnalysisStatus.Succeeded, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.Conflict, "IMAGE_ANALYSIS_NOT_REVIEWABLE", "The current image analysis is not available for review.");
        var raw = ReadFinalResult(analysis);
        ReviewedImageAnalysisProjection? projection = request.Disposition == ImageAnalysisReviewDecision.Rejected
            ? null
            : BuildReviewedProjection(analysis, raw, request);
        var editedFields = projection?.OfficerEditedFields ?? [];
        var review = new InspectionImageAnalysisReview
        {
            InspectionImageAnalysisId = analysis.Id,
            ReviewedByUserId = RequireUser(),
            Disposition = request.Disposition switch
            {
                ImageAnalysisReviewDecision.Accepted => ImageAnalysisReviewDisposition.Accepted,
                ImageAnalysisReviewDecision.Edited => ImageAnalysisReviewDisposition.Edited,
                _ => ImageAnalysisReviewDisposition.Rejected
            },
            ReviewedProjectionJson = projection is null ? null : JsonSerializer.Serialize(projection, JsonOptions),
            EditedFieldsJson = JsonSerializer.Serialize(editedFields, JsonOptions),
            StaffNote = string.IsNullOrWhiteSpace(request.StaffNote) ? null : request.StaffNote.Trim(),
            ReviewedAt = DateTime.UtcNow,
            CreatedByUserId = currentUser.UserId,
            UpdatedByUserId = currentUser.UserId
        };
        dbContext.InspectionImageAnalysisReviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return MapReview(review, projection);
    }

    public async Task<Guid?> ResolveEligibleReviewIdForSubmissionAsync(
        FieldInspection inspection,
        CropPlanRequest planRequest,
        ImageAnalysisCapabilityResponse? capability,
        CancellationToken cancellationToken)
    {
        if (capability is null) return null;
        var image = await CurrentRepresentativeAsync(inspection.Id, cancellationToken);
        if (image is null || string.IsNullOrWhiteSpace(image.ContentSha256)) return null;
        var fingerprint = BuildFingerprint(image, planRequest, capability);
        var analysis = await dbContext.InspectionImageAnalyses.AsNoTracking()
            .SingleOrDefaultAsync(item => item.FieldInspectionId == inspection.Id
                && item.InspectionImageId == image.Id
                && item.AnalysisFingerprint == fingerprint
                && item.Status == InspectionImageAnalysisStatus.Succeeded, cancellationToken);
        if (analysis is null) return null;
        var review = await dbContext.InspectionImageAnalysisReviews.AsNoTracking()
            .Where(item => item.InspectionImageAnalysisId == analysis.Id)
            .OrderByDescending(item => item.ReviewedAt)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return review?.Disposition is ImageAnalysisReviewDisposition.Accepted or ImageAnalysisReviewDisposition.Edited
            ? review.Id
            : null;
    }

    public async Task<ReviewedImageAnalysisProjection?> GetFrozenProjectionAsync(FieldInspection inspection, CancellationToken cancellationToken)
    {
        if (!inspection.FrozenImageAnalysisReviewId.HasValue) return null;
        var review = await dbContext.InspectionImageAnalysisReviews.AsNoTracking()
            .Include(item => item.InspectionImageAnalysis)
            .SingleOrDefaultAsync(item => item.Id == inspection.FrozenImageAnalysisReviewId.Value, cancellationToken);
        if (review?.InspectionImageAnalysis is null
            || review.InspectionImageAnalysis.FieldInspectionId != inspection.Id
            || review.Disposition is not (ImageAnalysisReviewDisposition.Accepted or ImageAnalysisReviewDisposition.Edited)
            || string.IsNullOrWhiteSpace(review.ReviewedProjectionJson)) return null;
        var projection = JsonSerializer.Deserialize<ReviewedImageAnalysisProjection>(review.ReviewedProjectionJson, JsonOptions);
        return projection is not null
            && projection.AnalysisId == review.InspectionImageAnalysisId
            && projection.InspectionImageId == review.InspectionImageAnalysis.InspectionImageId
            ? projection
            : null;
    }

    private async Task<InspectionImageAnalysisStateResponse> TerminalizeAsync(
        Guid analysisId,
        ImageAnalysisCapabilityResponse capability,
        InspectionImageAnalysisAiResponse? response,
        string? failureCategory,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var analysis = await dbContext.InspectionImageAnalyses.SingleAsync(item => item.Id == analysisId, cancellationToken);
        var inspection = await LockInspectionByIdAsync(analysis.FieldInspectionId, cancellationToken);
        if (analysis.Status != InspectionImageAnalysisStatus.Running)
            return await MapStateAsync(analysis, false, inspection.Status == InspectionStatus.Completed, cancellationToken);
        var planRequest = await LoadPlanRequestAsync(inspection.CropPlanRequestId!.Value, cancellationToken);
        var image = await CurrentRepresentativeAsync(inspection.Id, cancellationToken);
        var remainsCurrent = inspection.Status == InspectionStatus.InProgress
            && image is not null
            && image.Id == analysis.InspectionImageId
            && BuildFingerprint(image, planRequest, capability) == analysis.AnalysisFingerprint;
        if (!remainsCurrent)
        {
            analysis.Status = InspectionImageAnalysisStatus.Stale;
            analysis.FailureCategory = "context_changed_or_submitted";
            analysis.FailureMessageSafe = "The inspection or representative-image context changed while analysis was running.";
        }
        else if (response?.Status == "Succeeded" && response.FinalResult is not null
            && response.ContractVersion == InspectionImageAnalysisContract.Version)
        {
            analysis.Status = InspectionImageAnalysisStatus.Succeeded;
        }
        else if (response?.Status == "TimedOut" || failureCategory == "operation_timeout")
        {
            analysis.Status = InspectionImageAnalysisStatus.TimedOut;
        }
        else
        {
            analysis.Status = InspectionImageAnalysisStatus.Failed;
        }
        analysis.Pass1ResultJson = response?.Pass1Result is null ? null : JsonSerializer.Serialize(response.Pass1Result, JsonOptions);
        analysis.EvidencePacketJson = response is null ? null : JsonSerializer.Serialize(response.EvidencePacket, JsonOptions);
        analysis.FinalResultJson = response?.FinalResult is null ? null : JsonSerializer.Serialize(response.FinalResult, JsonOptions);
        analysis.FailureCategory ??= failureCategory ?? response?.FailureCategory;
        analysis.FailureMessageSafe ??= response?.FailureMessage;
        analysis.CompletedAt = DateTime.UtcNow;
        analysis.UpdatedAt = DateTime.UtcNow;
        analysis.Version = checked(analysis.Version + 1);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await MapStateAsync(analysis, remainsCurrent, !remainsCurrent, cancellationToken);
    }

    private async Task VerifyAndInitializeRepresentativeMetadataAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        var inspection = await dbContext.FieldInspections.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CropPlanRequestId == cropPlanRequestId && item.Purpose == InspectionPurpose.PrePlanting && !item.IsDeleted, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Pre-planting assessment was not found.");
        RequireDraftOwner(inspection);
        var snapshot = await CurrentRepresentativeAsync(inspection.Id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.Conflict, "REPRESENTATIVE_IMAGE_REQUIRED", "Select one representative inspection image before analysis.");
        var retrieved = await cloudinaryService.RetrieveInspectionImageAsync(snapshot.PublicId, snapshot.StorageVersion, snapshot.DeliveryType, cancellationToken);
        var actualHash = Convert.ToHexString(SHA256.HashData(retrieved.Bytes)).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(snapshot.ContentSha256) && !string.Equals(snapshot.ContentSha256, actualHash, StringComparison.OrdinalIgnoreCase))
            throw new ApiException(HttpStatusCode.Conflict, "IMAGE_INTEGRITY_FAILED", "Stored inspection image integrity verification failed.");

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var locked = await LockInspectionAsync(cropPlanRequestId, cancellationToken);
        RequireDraftOwner(locked);
        var current = await CurrentRepresentativeAsync(locked.Id, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.Conflict, "REPRESENTATIVE_IMAGE_CHANGED", "The representative image changed before analysis could start.");
        if (current.Id != snapshot.Id)
            throw new ApiException(HttpStatusCode.Conflict, "REPRESENTATIVE_IMAGE_CHANGED", "The representative image changed before analysis could start.");
        current.ContentSha256 ??= actualHash;
        current.StorageVersion ??= TryParseLegacyStorageVersion(current.Url);
        if (!current.StorageVersion.HasValue && string.IsNullOrWhiteSpace(current.AssetId))
            throw new ApiException(HttpStatusCode.Conflict, "LEGACY_IMAGE_METADATA_UNAVAILABLE", "The legacy image storage revision could not be established safely.");
        current.UpdatedAt = DateTime.UtcNow;
        current.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private static long? TryParseLegacyStorageVersion(string url)
    {
        foreach (var segment in url.Split('/', StringSplitOptions.RemoveEmptyEntries))
            if (segment.Length > 1 && segment[0] == 'v' && long.TryParse(segment[1..], out var version)) return version;
        return null;
    }

    private static string BuildFingerprint(InspectionImage image, CropPlanRequest planRequest, ImageAnalysisCapabilityResponse capability)
    {
        EnsureFingerprintMetadata(image);
        var canonical = string.Join('\n',
            image.Id.ToString("D"),
            image.AssetId ?? "legacy-no-asset-id",
            image.StorageVersion?.ToString() ?? "legacy-no-version",
            image.ContentSha256!.ToLowerInvariant(),
            planRequest.CropTypeId.ToString("D"),
            planRequest.CropVarietyId?.ToString("D") ?? "none",
            capability.SourcePolicyVersion,
            capability.SourcePolicyHash,
            capability.ContractVersion,
            capability.PromptContractVersion,
            capability.ImagePreprocessingVersion,
            capability.RelevanceRuleVersion,
            capability.Provider,
            capability.Model);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static void EnsureFingerprintMetadata(InspectionImage image)
    {
        if (string.IsNullOrWhiteSpace(image.ContentSha256))
            throw new ApiException(HttpStatusCode.Conflict, "IMAGE_METADATA_REQUIRED", "Image integrity metadata must be initialized before analysis.");
    }

    private static ReviewedImageAnalysisProjection BuildReviewedProjection(
        InspectionImageAnalysis analysis,
        InspectionImageAnalysisFinalResult raw,
        InspectionImageAnalysisReviewRequest request)
    {
        var edit = request.Disposition == ImageAnalysisReviewDecision.Edited
            ? request.EditedProjection ?? throw new ApiException(HttpStatusCode.BadRequest, "EDITED_PROJECTION_REQUIRED", "An edited projection is required.")
            : null;
        var findings = edit?.VisibleFindings ?? raw.VisibleFindings;
        var concerns = edit?.PossibleConcerns ?? raw.PossibleIssues;
        var severity = edit?.Severity ?? raw.Severity;
        var uncertainty = edit?.Uncertainty ?? raw.Uncertainty;
        var requiresFurtherAssessment = edit?.RequiresFurtherAssessment ?? raw.RequiresFurtherAssessment;
        var actionTypes = edit?.Actions ?? raw.RecommendedNonChemicalActions;
        var editedFields = new List<string>();
        if (edit is not null)
        {
            if (!findings.SequenceEqual(raw.VisibleFindings)) editedFields.Add("visibleFindings");
            if (!concerns.SequenceEqual(raw.PossibleIssues)) editedFields.Add("possibleConcerns");
            if (!string.Equals(severity, raw.Severity, StringComparison.Ordinal)) editedFields.Add("severity");
            if (!string.Equals(uncertainty, raw.Uncertainty, StringComparison.Ordinal)) editedFields.Add("uncertainty");
            if (!actionTypes.SequenceEqual(raw.RecommendedNonChemicalActions)) editedFields.Add("actions");
            if (requiresFurtherAssessment != raw.RequiresFurtherAssessment) editedFields.Add("requiresFurtherAssessment");
        }
        var sourceIds = raw.ValidatedSourceReferences.Select(item => item.SourcePolicyId).Distinct().ToArray();
        var actions = actionTypes.Select((action, index) => new ReviewedCropHealthAction(
            action,
            index,
            raw.RecommendedNonChemicalActions.Contains(action) ? "AiSuggested" : "OfficerAdded",
            raw.RecommendedNonChemicalActions.Contains(action) ? sourceIds : [])).ToArray();
        return new ReviewedImageAnalysisProjection(
            analysis.Id,
            analysis.InspectionImageId,
            findings,
            concerns,
            severity,
            uncertainty,
            actions,
            edit is null || editedFields.Count == 0 ? raw.ValidatedSourceReferences : [],
            requiresFurtherAssessment,
            editedFields);
    }

    private static void ValidateReviewRequest(InspectionImageAnalysisReviewRequest request)
    {
        if (request.StaffNote?.Length > 1000)
            throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", "The staff note must be 1000 characters or fewer.");
        if (request.Disposition != ImageAnalysisReviewDecision.Edited) return;
        var edit = request.EditedProjection ?? throw new ApiException(HttpStatusCode.BadRequest, "EDITED_PROJECTION_REQUIRED", "An edited projection is required.");
        if (edit.VisibleFindings.Count > 8 || edit.PossibleConcerns.Count > 5 || edit.Actions.Count > 7
            || edit.VisibleFindings.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 500)
            || edit.PossibleConcerns.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 500)
            || edit.Uncertainty.Length is < 1 or > 600
            || !new[] { "Low", "Moderate", "High", "Unknown" }.Contains(edit.Severity, StringComparer.Ordinal)
            || edit.Actions.Distinct().Count() != edit.Actions.Count)
            throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", "The edited image-analysis projection is invalid.");
        var text = string.Join(' ', edit.VisibleFindings.Concat(edit.PossibleConcerns).Append(edit.Uncertainty)).ToLowerInvariant();
        if (ProhibitedTreatmentTerms.Any(text.Contains))
            throw new ApiException(HttpStatusCode.BadRequest, "CHEMICAL_GUIDANCE_PROHIBITED", "Chemical treatment instructions are not allowed in this review path.");
        if (new[] { "confirmed disease", "confirmed pest", "definitive diagnosis", "definitely caused by", "diagnosed as" }.Any(text.Contains))
            throw new ApiException(HttpStatusCode.BadRequest, "UNSUPPORTED_DIAGNOSIS_CERTAINTY", "A single-image review must preserve diagnostic uncertainty.");
    }

    private InspectionImageAnalysisFinalResult ReadFinalResult(InspectionImageAnalysis analysis) =>
        !string.IsNullOrWhiteSpace(analysis.FinalResultJson)
            ? JsonSerializer.Deserialize<InspectionImageAnalysisFinalResult>(analysis.FinalResultJson, JsonOptions)
                ?? throw new ApiException(HttpStatusCode.Conflict, "IMAGE_ANALYSIS_RESULT_INVALID", "The image-analysis result is invalid.")
            : throw new ApiException(HttpStatusCode.Conflict, "IMAGE_ANALYSIS_RESULT_MISSING", "The image-analysis result is missing.");

    private async Task<InspectionImageAnalysisStateResponse> MapStateAsync(
        InspectionImageAnalysis analysis,
        bool isCurrent,
        bool isFrozen,
        CancellationToken cancellationToken)
    {
        var final = analysis.Status == InspectionImageAnalysisStatus.Succeeded ? ReadFinalResult(analysis) : null;
        var latest = await dbContext.InspectionImageAnalysisReviews.AsNoTracking()
            .Where(item => item.InspectionImageAnalysisId == analysis.Id)
            .OrderByDescending(item => item.ReviewedAt).ThenByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var review = latest is null ? null : MapReview(latest,
            string.IsNullOrWhiteSpace(latest.ReviewedProjectionJson)
                ? null
                : JsonSerializer.Deserialize<ReviewedImageAnalysisProjection>(latest.ReviewedProjectionJson, JsonOptions));
        return new InspectionImageAnalysisStateResponse(
            analysis.Id,
            analysis.Status.ToString(),
            isCurrent,
            isCurrent && !isFrozen && analysis.Status == InspectionImageAnalysisStatus.Succeeded,
            isFrozen,
            final,
            review,
            analysis.FailureCategory,
            analysis.FailureMessageSafe);
    }

    private static InspectionImageAnalysisReviewResponse MapReview(InspectionImageAnalysisReview review, ReviewedImageAnalysisProjection? projection) => new(
        review.Id,
        review.InspectionImageAnalysisId,
        review.Disposition switch
        {
            ImageAnalysisReviewDisposition.Accepted => ImageAnalysisReviewDecision.Accepted,
            ImageAnalysisReviewDisposition.Edited => ImageAnalysisReviewDecision.Edited,
            _ => ImageAnalysisReviewDecision.Rejected
        },
        projection,
        JsonSerializer.Deserialize<IReadOnlyList<string>>(review.EditedFieldsJson, JsonOptions) ?? [],
        review.StaffNote,
        review.ReviewedAt);

    private async Task<FieldInspection> VisibleInspectionAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        var inspection = await dbContext.FieldInspections.AsNoTracking().SingleOrDefaultAsync(item =>
            item.CropPlanRequestId == cropPlanRequestId && item.Purpose == InspectionPurpose.PrePlanting && !item.IsDeleted,
            cancellationToken) ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Pre-planting assessment was not found.");
        if (currentUser.Role == ApplicationRole.FieldOfficer && inspection.InspectorUserId != RequireUser())
            throw new ApiException(HttpStatusCode.Forbidden, "PREPLANT_ASSESSMENT_OWNER_REQUIRED", "This assessment belongs to another Field Officer.");
        if (currentUser.Role is not (ApplicationRole.FieldOfficer or ApplicationRole.AgriculturalOfficer or ApplicationRole.Admin))
            throw new ApiException(HttpStatusCode.Forbidden, "INSPECTION_EVIDENCE_VIEWER_REQUIRED", "Authorized inspection staff access is required.");
        return inspection;
    }

    private void RequireFieldOfficer()
    {
        if (currentUser.Role != ApplicationRole.FieldOfficer)
            throw new ApiException(HttpStatusCode.Forbidden, "FIELD_OFFICER_REQUIRED", "A Field Officer is required.");
    }

    private void RequireDraftOwner(FieldInspection inspection)
    {
        RequireFieldOfficer();
        if (inspection.InspectorUserId != RequireUser())
            throw new ApiException(HttpStatusCode.Forbidden, "PREPLANT_ASSESSMENT_OWNER_REQUIRED", "Only the owning Field Officer may change this assessment.");
        if (inspection.Status != InspectionStatus.InProgress)
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_IMMUTABLE", "A submitted assessment cannot be changed.");
    }

    private Guid RequireUser() => currentUser.UserId
        ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");

    private async Task<CropPlanRequest> LoadPlanRequestAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.CropPlanRequests.AsNoTracking()
            .Include(item => item.CropType)
            .Include(item => item.CropVariety)
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Crop plan request was not found.");

    private Task<InspectionImage?> CurrentRepresentativeAsync(Guid inspectionId, CancellationToken cancellationToken) =>
        dbContext.InspectionImages.SingleOrDefaultAsync(item => item.FieldInspectionId == inspectionId && item.IsRepresentativeForAi && !item.IsDeleted, cancellationToken);

    private async Task<FieldInspection> LockInspectionAsync(Guid cropPlanRequestId, CancellationToken cancellationToken)
    {
        var query = dbContext.Database.IsNpgsql()
            ? dbContext.FieldInspections.FromSqlInterpolated($@"SELECT * FROM ""FieldInspections"" WHERE ""CropPlanRequestId"" = {cropPlanRequestId} FOR UPDATE")
            : dbContext.FieldInspections;
        return await query.SingleOrDefaultAsync(item => item.CropPlanRequestId == cropPlanRequestId && item.Purpose == InspectionPurpose.PrePlanting && !item.IsDeleted, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Pre-planting assessment was not found.");
    }

    private async Task<FieldInspection> LockInspectionByIdAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        var query = dbContext.Database.IsNpgsql()
            ? dbContext.FieldInspections.FromSqlInterpolated($@"SELECT * FROM ""FieldInspections"" WHERE ""Id"" = {inspectionId} FOR UPDATE")
            : dbContext.FieldInspections;
        return await query.SingleAsync(item => item.Id == inspectionId, cancellationToken);
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

    private static InspectionImageAnalysisStateResponse Empty(string status, string message, bool frozen) =>
        new(null, status, false, false, frozen, null, null, null, message);

    private static InspectionImageAnalysisStateResponse Unavailable(string category, string message) =>
        new(null, "Unavailable", false, false, false, null, null, category, message);
}
