using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.Cloudinary;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Inspections;

public sealed class InspectionService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    ICloudinaryService cloudinaryService,
    IRequestValidator<FieldInspectionRequest> inspectionValidator,
    IRequestValidator<ObservationRequest> observationValidator,
    IRequestValidator<CropIssueRequest> issueValidator,
    IRequestValidator<FollowUpRecommendationRequest> recommendationValidator) : IInspectionService
{
    public async Task<PagedResult<FieldInspectionResponse>> SearchInspectionsAsync(PagedQuery query, Guid? fieldId, InspectionStatus? status, CancellationToken cancellationToken)
    {
        query.Normalize();
        var inspections = ApplyInspectionAccess(dbContext.FieldInspections.AsNoTracking());
        if (fieldId.HasValue) inspections = inspections.Where(item => item.FieldId == fieldId.Value);
        if (status.HasValue) inspections = inspections.Where(item => item.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            inspections = inspections.Where(item => item.Summary.ToLower().Contains(search));
        }

        inspections = query.SortBy?.ToLowerInvariant() switch
        {
            "status" => query.SortDirection == "desc" ? inspections.OrderByDescending(item => item.Status) : inspections.OrderBy(item => item.Status),
            "completedat" => query.SortDirection == "desc" ? inspections.OrderByDescending(item => item.CompletedAt) : inspections.OrderBy(item => item.CompletedAt),
            _ => query.SortDirection == "desc" ? inspections.OrderByDescending(item => item.ScheduledAt) : inspections.OrderBy(item => item.ScheduledAt)
        };
        var total = await inspections.CountAsync(cancellationToken);
        var items = await inspections.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapInspection(item)).ToListAsync(cancellationToken);
        return new PagedResult<FieldInspectionResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<FieldInspectionDetailResponse> GetInspectionAsync(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await ApplyInspectionAccess(dbContext.FieldInspections.AsNoTracking()).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Inspection");
        var observations = await dbContext.InspectionObservations.AsNoTracking().Where(item => item.FieldInspectionId == id && !item.IsDeleted).OrderBy(item => item.CreatedAt).Select(item => MapObservation(item)).ToListAsync(cancellationToken);
        var issues = await dbContext.CropIssues.AsNoTracking().Where(item => item.FieldInspectionId == id && !item.IsDeleted).OrderByDescending(item => item.CreatedAt).Select(item => MapIssue(item)).ToListAsync(cancellationToken);
        var images = await dbContext.InspectionImages.AsNoTracking().Where(item => item.FieldInspectionId == id && !item.IsDeleted).OrderByDescending(item => item.CreatedAt).Select(item => MapImage(item)).ToListAsync(cancellationToken);
        return new FieldInspectionDetailResponse(inspection.Id, inspection.FieldId, inspection.InspectorUserId, inspection.ScheduledAt, inspection.CompletedAt, inspection.Status, inspection.Summary, observations, issues, images);
    }

    public async Task<IReadOnlyList<InspectionHistoryEventResponse>> GetInspectionHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        var inspection = await ApplyInspectionAccess(dbContext.FieldInspections.AsNoTracking()).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Inspection");
        var events = new List<InspectionHistoryEventResponse>
        {
            new(inspection.CreatedAt, "InspectionCreated", inspection.Summary, inspection.Id)
        };
        if (inspection.CompletedAt.HasValue) events.Add(new(inspection.CompletedAt.Value, "InspectionSubmitted", inspection.Status.ToString(), inspection.Id));

        events.AddRange(await dbContext.InspectionObservations.AsNoTracking()
            .Where(item => item.FieldInspectionId == id && !item.IsDeleted)
            .Select(item => new InspectionHistoryEventResponse(item.CreatedAt, "Observation", item.ObservationType, item.Id))
            .ToListAsync(cancellationToken));
        events.AddRange(await dbContext.CropIssues.AsNoTracking()
            .Where(item => item.FieldInspectionId == id && !item.IsDeleted)
            .Select(item => new InspectionHistoryEventResponse(item.CreatedAt, "CropIssue", item.Title, item.Id))
            .ToListAsync(cancellationToken));
        events.AddRange(await dbContext.InspectionImages.AsNoTracking()
            .Where(item => item.FieldInspectionId == id && !item.IsDeleted)
            .Select(item => new InspectionHistoryEventResponse(item.CreatedAt, "ImageUploaded", item.ContentType, item.Id))
            .ToListAsync(cancellationToken));

        return events.OrderBy(item => item.OccurredAt).ToArray();
    }

    public async Task<FieldInspectionResponse> CreateInspectionAsync(FieldInspectionRequest request, CancellationToken cancellationToken)
    {
        Validate(inspectionValidator.Validate(request));
        RequireInspectionStaff();
        await EnsureFieldVisibleAsync(request.FieldId, cancellationToken);
        var inspection = new FieldInspection { FieldId = request.FieldId, InspectorUserId = RequireUser(), ScheduledAt = request.ScheduledAt, Status = request.Status, Summary = request.Summary.Trim(), CreatedByUserId = currentUser.UserId };
        if (request.Status == InspectionStatus.Completed) inspection.CompletedAt = DateTime.UtcNow;
        dbContext.FieldInspections.Add(inspection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapInspection(inspection);
    }

    public async Task<FieldInspectionResponse> UpdateInspectionAsync(Guid id, FieldInspectionRequest request, CancellationToken cancellationToken)
    {
        Validate(inspectionValidator.Validate(request));
        RequireInspectionStaff();
        var inspection = await ApplyInspectionAccess(dbContext.FieldInspections).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Inspection");
        inspection.ScheduledAt = request.ScheduledAt;
        inspection.Status = request.Status;
        inspection.Summary = request.Summary.Trim();
        inspection.CompletedAt = request.Status == InspectionStatus.Completed ? DateTime.UtcNow : inspection.CompletedAt;
        inspection.UpdatedAt = DateTime.UtcNow;
        inspection.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapInspection(inspection);
    }

    public Task<FieldInspectionResponse> SubmitInspectionAsync(Guid id, CancellationToken cancellationToken) => SetInspectionStatusAsync(id, InspectionStatus.Completed, cancellationToken);

    public Task<FieldInspectionResponse> CloseInspectionAsync(Guid id, CancellationToken cancellationToken) => SetInspectionStatusAsync(id, InspectionStatus.Cancelled, cancellationToken);

    public async Task<PagedResult<ObservationResponse>> SearchObservationsAsync(PagedQuery query, Guid? inspectionId, CancellationToken cancellationToken)
    {
        query.Normalize();
        var observations = dbContext.InspectionObservations.AsNoTracking()
            .Include(item => item.FieldInspection)!.ThenInclude(inspection => inspection!.Field)!.ThenInclude(field => field!.Farm)
            .Where(item => !item.IsDeleted);
        if (currentUser.Role == ApplicationRole.Farmer) observations = observations.Where(item => item.FieldInspection!.Field!.Farm!.OwnerUserId == currentUser.UserId);
        if (inspectionId.HasValue) observations = observations.Where(item => item.FieldInspectionId == inspectionId.Value);
        observations = observations.OrderBy(item => item.CreatedAt);
        var total = await observations.CountAsync(cancellationToken);
        var items = await observations.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapObservation(item)).ToListAsync(cancellationToken);
        return new PagedResult<ObservationResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<ObservationResponse> CreateObservationAsync(ObservationRequest request, CancellationToken cancellationToken)
    {
        Validate(observationValidator.Validate(request));
        RequireInspectionStaff();
        await EnsureInspectionVisibleAsync(request.FieldInspectionId, cancellationToken);
        var observation = new InspectionObservation { FieldInspectionId = request.FieldInspectionId, ObservationType = request.ObservationType.Trim(), Notes = request.Notes.Trim(), CreatedByUserId = currentUser.UserId };
        dbContext.InspectionObservations.Add(observation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapObservation(observation);
    }

    public async Task<PagedResult<CropIssueResponse>> SearchIssuesAsync(PagedQuery query, CropIssueSeverity? severity, CropIssueStatus? status, CancellationToken cancellationToken)
    {
        query.Normalize();
        var issues = ApplyIssueAccess(dbContext.CropIssues.AsNoTracking());
        if (severity.HasValue) issues = issues.Where(item => item.Severity == severity.Value);
        if (status.HasValue) issues = issues.Where(item => item.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            issues = issues.Where(item => item.Title.ToLower().Contains(search) || item.Description.ToLower().Contains(search));
        }

        issues = query.SortBy?.ToLowerInvariant() switch
        {
            "severity" => query.SortDirection == "desc" ? issues.OrderByDescending(item => item.Severity) : issues.OrderBy(item => item.Severity),
            "status" => query.SortDirection == "desc" ? issues.OrderByDescending(item => item.Status) : issues.OrderBy(item => item.Status),
            _ => query.SortDirection == "desc" ? issues.OrderByDescending(item => item.CreatedAt) : issues.OrderBy(item => item.CreatedAt)
        };
        var total = await issues.CountAsync(cancellationToken);
        var items = await issues.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapIssue(item)).ToListAsync(cancellationToken);
        return new PagedResult<CropIssueResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<CropIssueResponse> GetIssueAsync(Guid id, CancellationToken cancellationToken)
    {
        var issue = await ApplyIssueAccess(dbContext.CropIssues.AsNoTracking()).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Crop issue");
        return MapIssue(issue);
    }

    public async Task<CropIssueResponse> CreateIssueAsync(CropIssueRequest request, CancellationToken cancellationToken)
    {
        Validate(issueValidator.Validate(request));
        RequireInspectionStaff();
        await EnsureInspectionVisibleAsync(request.FieldInspectionId, cancellationToken);
        if (request.Status == CropIssueStatus.Escalated && request.Severity is CropIssueSeverity.Low or CropIssueSeverity.Medium)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "ISSUE_NOT_SERIOUS", "Only high or critical crop issues can be escalated.");
        }
        var issue = new CropIssue { FieldInspectionId = request.FieldInspectionId, Title = request.Title.Trim(), Description = request.Description.Trim(), Severity = request.Severity, Status = request.Status, CreatedByUserId = currentUser.UserId };
        if (request.Status == CropIssueStatus.Escalated) issue.EscalatedAt = DateTime.UtcNow;
        dbContext.CropIssues.Add(issue);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapIssue(issue);
    }

    public async Task<CropIssueResponse> UpdateIssueStatusAsync(Guid issueId, CropIssueStatusRequest request, CancellationToken cancellationToken)
    {
        RequireInspectionStaff();
        if (!Enum.IsDefined(request.Severity) || !Enum.IsDefined(request.Status)) throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", "Issue severity or status is invalid.");
        var issue = await ApplyIssueAccess(dbContext.CropIssues).SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken) ?? throw NotFound("Crop issue");
        if (request.Status == CropIssueStatus.Escalated && request.Severity is CropIssueSeverity.Low or CropIssueSeverity.Medium)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "ISSUE_NOT_SERIOUS", "Only high or critical crop issues can be escalated.");
        }
        issue.Severity = request.Severity;
        issue.Status = request.Status;
        if (request.Status == CropIssueStatus.Escalated && !issue.EscalatedAt.HasValue)
        {
            issue.EscalatedAt = DateTime.UtcNow;
            issue.EscalatedToUserId = currentUser.UserId;
        }
        issue.UpdatedAt = DateTime.UtcNow;
        issue.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapIssue(issue);
    }

    public async Task<CropIssueResponse> EscalateIssueAsync(Guid issueId, CancellationToken cancellationToken)
    {
        RequireInspectionStaff();
        var issue = await ApplyIssueAccess(dbContext.CropIssues).SingleOrDefaultAsync(item => item.Id == issueId, cancellationToken) ?? throw NotFound("Crop issue");
        if (issue.Severity is CropIssueSeverity.Low or CropIssueSeverity.Medium)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "ISSUE_NOT_SERIOUS", "Only high or critical crop issues can be escalated.");
        }

        issue.Status = CropIssueStatus.Escalated;
        issue.EscalatedAt ??= DateTime.UtcNow;
        issue.EscalatedToUserId = currentUser.UserId;
        issue.UpdatedAt = DateTime.UtcNow;
        issue.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapIssue(issue);
    }

    public async Task<PagedResult<FollowUpRecommendationResponse>> SearchRecommendationsAsync(PagedQuery query, Guid? cropIssueId, bool? isCompleted, CancellationToken cancellationToken)
    {
        query.Normalize();
        var recommendations = dbContext.FollowUpRecommendations.AsNoTracking()
            .Include(item => item.CropIssue)!.ThenInclude(issue => issue!.FieldInspection)!.ThenInclude(inspection => inspection!.Field)!.ThenInclude(field => field!.Farm)
            .Where(item => !item.IsDeleted);
        if (currentUser.Role == ApplicationRole.Farmer) recommendations = recommendations.Where(item => item.CropIssue!.FieldInspection!.Field!.Farm!.OwnerUserId == currentUser.UserId);
        if (cropIssueId.HasValue) recommendations = recommendations.Where(item => item.CropIssueId == cropIssueId.Value);
        if (isCompleted.HasValue) recommendations = recommendations.Where(item => item.IsCompleted == isCompleted.Value);
        recommendations = query.SortDirection == "desc" ? recommendations.OrderByDescending(item => item.DueAt ?? item.CreatedAt) : recommendations.OrderBy(item => item.DueAt ?? item.CreatedAt);
        var total = await recommendations.CountAsync(cancellationToken);
        var items = await recommendations.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapRecommendation(item)).ToListAsync(cancellationToken);
        return new PagedResult<FollowUpRecommendationResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<FollowUpRecommendationResponse> CreateRecommendationAsync(FollowUpRecommendationRequest request, CancellationToken cancellationToken)
    {
        Validate(recommendationValidator.Validate(request));
        RequireInspectionStaff();
        if (!await ApplyIssueAccess(dbContext.CropIssues.AsNoTracking()).AnyAsync(item => item.Id == request.CropIssueId, cancellationToken)) throw NotFound("Crop issue");
        var recommendation = new FollowUpRecommendation { CropIssueId = request.CropIssueId, Recommendation = request.Recommendation.Trim(), DueAt = request.DueAt, IsCompleted = request.IsCompleted, CreatedByUserId = currentUser.UserId };
        dbContext.FollowUpRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRecommendation(recommendation);
    }

    public async Task<FollowUpRecommendationResponse> UpdateRecommendationAsync(Guid id, FollowUpRecommendationUpdateRequest request, CancellationToken cancellationToken)
    {
        RequireInspectionStaff();
        var recommendation = await dbContext.FollowUpRecommendations
            .Include(item => item.CropIssue)!.ThenInclude(issue => issue!.FieldInspection)!.ThenInclude(inspection => inspection!.Field)!.ThenInclude(field => field!.Farm)
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw NotFound("Follow-up recommendation");
        if (currentUser.Role == ApplicationRole.Farmer && recommendation.CropIssue!.FieldInspection!.Field!.Farm!.OwnerUserId != currentUser.UserId) throw NotFound("Follow-up recommendation");
        recommendation.IsCompleted = request.IsCompleted;
        recommendation.UpdatedAt = DateTime.UtcNow;
        recommendation.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRecommendation(recommendation);
    }

    public async Task<InspectionImageResponse> UploadImageAsync(Guid inspectionId, IFormFile file, CancellationToken cancellationToken)
    {
        RequireInspectionStaff();
        await EnsureInspectionVisibleAsync(inspectionId, cancellationToken);
        var upload = await cloudinaryService.UploadInspectionImageAsync(file, cancellationToken);
        var image = new InspectionImage { FieldInspectionId = inspectionId, Url = upload.Url, PublicId = upload.PublicId, ContentType = upload.ContentType, SizeBytes = upload.SizeBytes, CreatedByUserId = currentUser.UserId };
        dbContext.InspectionImages.Add(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapImage(image);
    }

    public async Task<IReadOnlyList<InspectionImageResponse>> GetInspectionImagesAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        await EnsureInspectionVisibleAsync(inspectionId, cancellationToken);
        return await dbContext.InspectionImages.AsNoTracking()
            .Where(item => item.FieldInspectionId == inspectionId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => MapImage(item))
            .ToListAsync(cancellationToken);
    }

    private async Task<FieldInspectionResponse> SetInspectionStatusAsync(Guid id, InspectionStatus status, CancellationToken cancellationToken)
    {
        RequireInspectionStaff();
        var inspection = await ApplyInspectionAccess(dbContext.FieldInspections).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Inspection");
        inspection.Status = status;
        inspection.CompletedAt = status == InspectionStatus.Completed ? DateTime.UtcNow : inspection.CompletedAt;
        inspection.UpdatedAt = DateTime.UtcNow;
        inspection.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapInspection(inspection);
    }

    private IQueryable<FieldInspection> ApplyInspectionAccess(IQueryable<FieldInspection> query)
    {
        query = query.Include(item => item.Field)!.ThenInclude(field => field!.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.Field!.Farm!.OwnerUserId == currentUser.UserId) : query;
    }

    private IQueryable<CropIssue> ApplyIssueAccess(IQueryable<CropIssue> query)
    {
        query = query.Include(item => item.FieldInspection)!.ThenInclude(inspection => inspection!.Field)!.ThenInclude(field => field!.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.FieldInspection!.Field!.Farm!.OwnerUserId == currentUser.UserId) : query;
    }

    private async Task EnsureFieldVisibleAsync(Guid fieldId, CancellationToken cancellationToken)
    {
        var fields = dbContext.Fields.Include(item => item.Farm).AsNoTracking().Where(item => !item.IsDeleted && item.Farm != null && !item.Farm.IsDeleted);
        if (currentUser.Role == ApplicationRole.Farmer) fields = fields.Where(item => item.Farm!.OwnerUserId == currentUser.UserId);
        if (!await fields.AnyAsync(item => item.Id == fieldId, cancellationToken)) throw NotFound("Field");
    }

    private async Task EnsureInspectionVisibleAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        if (!await ApplyInspectionAccess(dbContext.FieldInspections.AsNoTracking()).AnyAsync(item => item.Id == inspectionId, cancellationToken)) throw NotFound("Inspection");
    }

    private void RequireInspectionStaff()
    {
        if (currentUser.Role is not (ApplicationRole.FieldOfficer or ApplicationRole.AgriculturalOfficer or ApplicationRole.Admin))
        {
            throw new ApiException(HttpStatusCode.Forbidden, "INSPECTION_STAFF_REQUIRED", "Inspection staff role is required.");
        }
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");
    private static ApiException NotFound(string name) => new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");
    private static void Validate(IReadOnlyList<string> errors) { if (errors.Count > 0) throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors)); }
    private static FieldInspectionResponse MapInspection(FieldInspection item) => new(item.Id, item.FieldId, item.InspectorUserId, item.ScheduledAt, item.CompletedAt, item.Status, item.Summary);
    private static ObservationResponse MapObservation(InspectionObservation item) => new(item.Id, item.FieldInspectionId, item.ObservationType, item.Notes);
    private static CropIssueResponse MapIssue(CropIssue item) => new(item.Id, item.FieldInspectionId, item.Title, item.Description, item.Severity, item.Status, item.EscalatedAt);
    private static FollowUpRecommendationResponse MapRecommendation(FollowUpRecommendation item) => new(item.Id, item.CropIssueId, item.Recommendation, item.DueAt, item.IsCompleted);
    private static InspectionImageResponse MapImage(InspectionImage item) => new(item.Id, item.FieldInspectionId, item.Url, item.PublicId, item.ContentType, item.SizeBytes);
}
