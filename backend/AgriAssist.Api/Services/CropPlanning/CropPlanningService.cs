using System.Net;
using System.Text.Json;
using System.Data;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgriAssist.Api.Services.CropPlanning;

public sealed class CropPlanningService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IRequestValidator<FarmRequest> farmValidator,
    IRequestValidator<FieldRequest> fieldValidator,
    IRequestValidator<CropTypeRequest> cropTypeValidator,
    IRequestValidator<CropCycleRequest> cropCycleValidator,
    IRequestValidator<CropPlanRequestCreate> createRequestValidator,
    IRequestValidator<CropPlanRequestUpdate> updateRequestValidator,
    IRequestValidator<PrePlantingAssessmentRequest> prePlantingAssessmentValidator,
    IAgenticAIClient agenticAIClient) : ICropPlanningService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string CoordinatorAgentName = "CropPlanningCoordinatorAgent";
    private const string CoordinatorStepName = "CropPlanningCoordinator";
    private const string FieldAnalysisAgentName = "CropFieldAnalysisAgent";
    private const string FieldAnalysisStepName = "FieldAnalysis";
    private const string WeatherResourceAgentName = "WeatherResourceAgent";
    private const string RiskAssessmentObservationType = "IdentifiedRisksAssessment";
    private const string RiskObservationType = "IdentifiedRisk";
    private const string RiskAssessmentCompleteValue = "Assessed";

    public CropPlanningService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IRequestValidator<FarmRequest> farmValidator,
        IRequestValidator<FieldRequest> fieldValidator,
        IRequestValidator<CropTypeRequest> cropTypeValidator,
        IRequestValidator<CropCycleRequest> cropCycleValidator,
        IRequestValidator<CropPlanRequestCreate> createRequestValidator,
        IRequestValidator<CropPlanRequestUpdate> updateRequestValidator)
        : this(dbContext, currentUser, farmValidator, fieldValidator, cropTypeValidator, cropCycleValidator, createRequestValidator, updateRequestValidator, new PrePlantingAssessmentRequestValidator(), new UnavailableAgenticAIClient())
    {
    }

    public CropPlanningService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IRequestValidator<FarmRequest> farmValidator,
        IRequestValidator<FieldRequest> fieldValidator,
        IRequestValidator<CropTypeRequest> cropTypeValidator,
        IRequestValidator<CropCycleRequest> cropCycleValidator,
        IRequestValidator<CropPlanRequestCreate> createRequestValidator,
        IRequestValidator<CropPlanRequestUpdate> updateRequestValidator,
        IAgenticAIClient agenticAIClient)
        : this(dbContext, currentUser, farmValidator, fieldValidator, cropTypeValidator, cropCycleValidator, createRequestValidator, updateRequestValidator, new PrePlantingAssessmentRequestValidator(), agenticAIClient)
    {
    }

    public async Task<FarmerOnboardingStatusResponse> GetFarmerOnboardingStatusAsync(CancellationToken cancellationToken)
    {
        var farmerId = RequireUser();
        if (!currentUser.IsInRole(ApplicationRole.Farmer))
        {
            throw new ApiException(HttpStatusCode.Forbidden, "FARMER_REQUIRED", "A Farmer account is required.");
        }

        var ownedFarmIds = dbContext.Farms.AsNoTracking()
            .Where(farm => farm.OwnerUserId == farmerId && !farm.IsDeleted)
            .Select(farm => farm.Id);
        var activeFarmCount = await ownedFarmIds.CountAsync(cancellationToken);
        var activeFieldCount = await dbContext.Fields.AsNoTracking()
            .CountAsync(field =>
                ownedFarmIds.Contains(field.FarmId) &&
                field.IsActive &&
                !field.IsDeleted,
                cancellationToken);

        var stage = activeFarmCount == 0
            ? "farm"
            : activeFieldCount == 0
                ? "field"
                : "complete";

        return new FarmerOnboardingStatusResponse(stage, activeFarmCount, activeFieldCount);
    }

    public async Task<PagedResult<FarmResponse>> SearchFarmsAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var farms = ApplyFarmAccess(dbContext.Farms.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            farms = farms.Where(farm => farm.Name.ToLower().Contains(search) || farm.Location.ToLower().Contains(search));
        }

        farms = query.SortBy?.ToLowerInvariant() switch
        {
            "location" => query.SortDirection == "desc" ? farms.OrderByDescending(farm => farm.Location) : farms.OrderBy(farm => farm.Location),
            "createdat" => query.SortDirection == "desc" ? farms.OrderByDescending(farm => farm.CreatedAt) : farms.OrderBy(farm => farm.CreatedAt),
            _ => query.SortDirection == "desc" ? farms.OrderByDescending(farm => farm.Name) : farms.OrderBy(farm => farm.Name)
        };

        var total = await farms.CountAsync(cancellationToken);
        var items = await farms.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(farm => MapFarm(farm)).ToListAsync(cancellationToken);
        return new PagedResult<FarmResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<FarmResponse> GetFarmAsync(Guid id, CancellationToken cancellationToken)
    {
        var farm = await ApplyFarmAccess(dbContext.Farms.AsNoTracking()).SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound("Farm");
        return MapFarm(farm);
    }

    public async Task<FarmResponse> CreateFarmAsync(FarmRequest request, CancellationToken cancellationToken)
    {
        Validate(farmValidator.Validate(request));
        var ownerId = currentUser.IsInRole(ApplicationRole.Admin) && request.OwnerUserId.HasValue
            ? request.OwnerUserId.Value
            : RequireUser();

        var owner = await dbContext.Users.AnyAsync(user => user.Id == ownerId && user.Role == ApplicationRole.Farmer && user.IsActive, cancellationToken);
        if (!owner)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "INVALID_FARM_OWNER", "Farm owner must be an active Farmer user.");
        }

        var farm = new Farm { Name = request.Name.Trim(), Location = request.Location.Trim(), TotalArea = request.TotalArea, OwnerUserId = ownerId, CreatedByUserId = currentUser.UserId };
        dbContext.Farms.Add(farm);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapFarm(farm);
    }

    public async Task<FarmResponse> UpdateFarmAsync(Guid id, FarmRequest request, CancellationToken cancellationToken)
    {
        Validate(farmValidator.Validate(request));
        await using var transaction = await BeginCapacityTransactionAsync(cancellationToken);
        var farm = await LockFarmForCapacityAsync(id, cancellationToken);
        var allocatedArea = await GetAllocatedFieldAreaAsync(id, cancellationToken: cancellationToken);
        if (allocatedArea > request.TotalArea)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "FIELD_AREA_EXCEEDS_FARM", "Farm area cannot be less than its total active field area.");
        }

        farm.Name = request.Name.Trim();
        farm.Location = request.Location.Trim();
        farm.TotalArea = request.TotalArea;
        farm.UpdatedAt = DateTime.UtcNow;
        farm.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return MapFarm(farm);
    }

    public async Task DeleteFarmAsync(Guid id, CancellationToken cancellationToken)
    {
        var farm = await ApplyFarmAccess(dbContext.Farms).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Farm");
        farm.IsDeleted = true;
        farm.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<FieldResponse>> SearchFieldsAsync(PagedQuery query, Guid? farmId, CancellationToken cancellationToken)
    {
        query.Normalize();
        var fields = ApplyFieldAccess(dbContext.Fields.AsNoTracking());
        if (farmId.HasValue) fields = fields.Where(field => field.FarmId == farmId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            fields = fields.Where(field => field.Name.ToLower().Contains(search) || field.SoilType.ToLower().Contains(search));
        }

        fields = query.SortDirection == "desc" ? fields.OrderByDescending(field => field.Name) : fields.OrderBy(field => field.Name);
        var total = await fields.CountAsync(cancellationToken);
        var items = await fields.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(field => MapField(field)).ToListAsync(cancellationToken);
        return new PagedResult<FieldResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<FieldResponse> CreateFieldAsync(FieldRequest request, CancellationToken cancellationToken)
    {
        Validate(fieldValidator.Validate(request));
        await using var transaction = await BeginCapacityTransactionAsync(cancellationToken);
        var farm = await LockFarmForCapacityAsync(request.FarmId, cancellationToken);
        var allocatedArea = await GetAllocatedFieldAreaAsync(request.FarmId, cancellationToken: cancellationToken);
        var newAllocation = request.IsActive ? request.Area : 0m;
        if (allocatedArea + newAllocation > farm.TotalArea)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "FIELD_AREA_EXCEEDS_FARM", "Total active field area cannot exceed farm area.");
        }

        var field = new Field { FarmId = request.FarmId, Name = request.Name.Trim(), Area = request.Area, SoilType = request.SoilType.Trim(), IsActive = request.IsActive, CreatedByUserId = currentUser.UserId };
        dbContext.Fields.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return MapField(field);
    }

    public async Task<FieldResponse> UpdateFieldAsync(Guid id, FieldRequest request, CancellationToken cancellationToken)
    {
        Validate(fieldValidator.Validate(request));
        await using var transaction = await BeginCapacityTransactionAsync(cancellationToken);
        var field = await ApplyFieldAccess(dbContext.Fields).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Field");
        var farm = await LockFarmForCapacityAsync(field.FarmId, cancellationToken);
        var allocatedArea = await GetAllocatedFieldAreaAsync(field.FarmId, id, cancellationToken);
        var updatedAllocation = request.IsActive ? request.Area : 0m;
        if (allocatedArea + updatedAllocation > farm.TotalArea)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "FIELD_AREA_EXCEEDS_FARM", "Total active field area cannot exceed farm area.");
        }

        field.Name = request.Name.Trim();
        field.Area = request.Area;
        field.SoilType = request.SoilType.Trim();
        field.IsActive = request.IsActive;
        field.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return MapField(field);
    }

    public async Task<PagedResult<CropTypeResponse>> SearchCropTypesAsync(PagedQuery query, CancellationToken cancellationToken, bool includeInactive = false)
    {
        query.Normalize();
        var cropTypes = dbContext.CropTypes.AsNoTracking().Where(item => !item.IsDeleted);
        if (includeInactive)
        {
            RequireAdmin();
        }
        else
        {
            cropTypes = cropTypes.Where(item => item.IsActive);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            cropTypes = cropTypes.Where(item => item.Name.ToLower().Contains(search));
        }

        cropTypes = query.SortDirection == "desc" ? cropTypes.OrderByDescending(item => item.Name) : cropTypes.OrderBy(item => item.Name);
        var total = await cropTypes.CountAsync(cancellationToken);
        var items = await cropTypes.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapCropType(item)).ToListAsync(cancellationToken);
        return new PagedResult<CropTypeResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<CropTypeResponse> CreateCropTypeAsync(CropTypeRequest request, CancellationToken cancellationToken)
    {
        Validate(cropTypeValidator.Validate(request));
        RequireAdmin();
        var duplicate = await dbContext.CropTypes.AnyAsync(item => item.Name.ToLower() == request.Name.Trim().ToLower(), cancellationToken);
        if (duplicate) throw new ApiException(HttpStatusCode.Conflict, "CROP_TYPE_EXISTS", "Crop type already exists.");
        var cropType = new CropType { Name = request.Name.Trim(), Description = request.Description?.Trim(), IsActive = request.IsActive, CreatedByUserId = currentUser.UserId };
        dbContext.CropTypes.Add(cropType);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapCropType(cropType);
    }

    public async Task<CropTypeResponse> UpdateCropTypeAsync(Guid id, CropTypeRequest request, CancellationToken cancellationToken)
    {
        Validate(cropTypeValidator.Validate(request));
        RequireAdmin();
        var cropType = await dbContext.CropTypes.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw NotFound("Crop type");
        var duplicate = await dbContext.CropTypes.AnyAsync(item => item.Id != id && item.Name.ToLower() == request.Name.Trim().ToLower(), cancellationToken);
        if (duplicate) throw new ApiException(HttpStatusCode.Conflict, "CROP_TYPE_EXISTS", "Crop type already exists.");
        cropType.Name = request.Name.Trim();
        cropType.Description = request.Description?.Trim();
        cropType.IsActive = request.IsActive;
        cropType.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapCropType(cropType);
    }

    public async Task<PagedResult<CropVarietyResponse>> SearchCropVarietiesAsync(PagedQuery query, Guid? cropTypeId, CancellationToken cancellationToken, bool includeInactive = false)
    {
        query.Normalize();
        var varieties = dbContext.CropVarieties.AsNoTracking().Include(item => item.CropType).Where(item => !item.IsDeleted);
        if (includeInactive) RequireAdmin();
        else varieties = varieties.Where(item => item.IsActive && item.CropType != null && item.CropType.IsActive && !item.CropType.IsDeleted);
        if (cropTypeId.HasValue) varieties = varieties.Where(item => item.CropTypeId == cropTypeId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            varieties = varieties.Where(item => item.Name.ToLower().Contains(search));
        }
        varieties = query.SortDirection == "desc" ? varieties.OrderByDescending(item => item.Name) : varieties.OrderBy(item => item.Name);
        var total = await varieties.CountAsync(cancellationToken);
        var items = await varieties.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new CropVarietyResponse(item.Id, item.CropTypeId, item.Name, item.IsActive)).ToListAsync(cancellationToken);
        return new PagedResult<CropVarietyResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<CropVarietyResponse> CreateCropVarietyAsync(CropVarietyRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        Validate(new CropVarietyRequestValidator().Validate(request));
        await EnsureCropTypeAsync(request.CropTypeId, cancellationToken);
        var name = request.Name.Trim();
        if (await dbContext.CropVarieties.AnyAsync(item => item.CropTypeId == request.CropTypeId && item.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ApiException(HttpStatusCode.Conflict, "CROP_VARIETY_EXISTS", "Crop variety already exists.");
        var variety = new CropVariety { CropTypeId = request.CropTypeId, Name = name, IsActive = request.IsActive, CreatedByUserId = currentUser.UserId };
        dbContext.CropVarieties.Add(variety);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapVariety(variety);
    }

    public async Task<CropVarietyResponse> UpdateCropVarietyAsync(Guid id, CropVarietyRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        Validate(new CropVarietyRequestValidator().Validate(request));
        var variety = await dbContext.CropVarieties.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw NotFound("Crop variety");
        if (variety.CropTypeId != request.CropTypeId)
            throw new ApiException(HttpStatusCode.BadRequest, "VARIETY_CROP_IMMUTABLE", "A variety cannot be moved to another crop type.");
        var name = request.Name.Trim();
        if (!name.Equals(variety.Name, StringComparison.OrdinalIgnoreCase))
        {
            if (await dbContext.CropVarieties.AnyAsync(item => item.Id != id && item.CropTypeId == request.CropTypeId && item.Name.ToLower() == name.ToLower(), cancellationToken))
                throw new ApiException(HttpStatusCode.Conflict, "CROP_VARIETY_EXISTS", "Crop variety already exists.");
            if (await dbContext.CropReferenceProfiles.AnyAsync(item => item.CropTypeId == request.CropTypeId && item.VarietyName == variety.Name, cancellationToken))
                throw new ApiException(HttpStatusCode.Conflict, "VARIETY_REFERENCED", "Deactivate this variety instead of renaming it because reference data already uses its name.");
            variety.Name = name;
        }
        variety.IsActive = request.IsActive;
        variety.UpdatedAt = DateTime.UtcNow;
        variety.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapVariety(variety);
    }

    public async Task<PagedResult<CropReferenceProfileResponse>> SearchReferenceProfilesAsync(PagedQuery query, Guid? cropTypeId, CancellationToken cancellationToken)
    {
        RequireAdmin();
        query.Normalize();
        var profiles = dbContext.CropReferenceProfiles.AsNoTracking().Where(item => !item.IsDeleted);
        if (cropTypeId.HasValue) profiles = profiles.Where(item => item.CropTypeId == cropTypeId.Value);
        profiles = profiles.OrderByDescending(item => item.VerifiedAt);
        var total = await profiles.CountAsync(cancellationToken);
        var items = await profiles.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new CropReferenceProfileResponse(item.Id, item.CropTypeId, item.VarietyName, item.Region,
                item.SourceName, item.SourceUrl, item.SourceVersion, item.VerifiedAt, item.IsActive,
                item.Stages.Count, item.Rules.Count)).ToListAsync(cancellationToken);
        return new PagedResult<CropReferenceProfileResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<CropReferenceProfileResponse> CreateReferenceProfileAsync(CropReferenceProfileRequest request, CancellationToken cancellationToken)
    {
        RequireAdmin();
        Validate(new CropReferenceProfileRequestValidator().Validate(request));
        await EnsureCropTypeAsync(request.CropTypeId, cancellationToken);
        string? varietyName = null;
        if (request.CropVarietyId.HasValue)
        {
            varietyName = await dbContext.CropVarieties.AsNoTracking()
                .Where(item => item.Id == request.CropVarietyId.Value && item.CropTypeId == request.CropTypeId && item.IsActive && !item.IsDeleted)
                .Select(item => item.Name).SingleOrDefaultAsync(cancellationToken)
                ?? throw new ApiException(HttpStatusCode.BadRequest, "INVALID_CROP_VARIETY", "Selected variety is not active for this crop.");
        }
        var verifiedAt = request.VerifiedAt.ToUniversalTime();
        var profile = new CropReferenceProfile
        {
            CropTypeId = request.CropTypeId, VarietyName = varietyName, Region = request.Region?.Trim(),
            SourceName = request.SourceName.Trim(), SourceUrl = request.SourceUrl?.Trim(),
            SourceVersion = request.SourceVersion.Trim(), VerifiedAt = verifiedAt,
            CreatedByUserId = currentUser.UserId,
            Stages = request.Stages.Select(item => new CropStageReference
            {
                StageName = item.StageName.Trim(), Sequence = item.Sequence, TypicalMinDays = item.TypicalMinDays,
                TypicalMaxDays = item.TypicalMaxDays, Notes = item.Notes?.Trim(), SourceName = request.SourceName.Trim(),
                SourceUrl = request.SourceUrl?.Trim(), CreatedByUserId = currentUser.UserId
            }).ToList(),
            Rules = request.Rules.Select(item => new CropRuleReference
            {
                RuleType = item.RuleType.Trim(), RuleKey = item.RuleKey.Trim(), StructuredValueJson = item.StructuredValueJson,
                SourceName = request.SourceName.Trim(), SourceUrl = request.SourceUrl?.Trim(), VerifiedAt = verifiedAt,
                CreatedByUserId = currentUser.UserId
            }).ToList()
        };
        dbContext.CropReferenceProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapReferenceProfile(profile);
    }

    public async Task<CropReferenceProfileResponse> SetReferenceProfileActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        RequireAdmin();
        var profile = await dbContext.CropReferenceProfiles.Include(item => item.Stages).Include(item => item.Rules)
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw NotFound("Crop reference profile");
        profile.IsActive = isActive;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapReferenceProfile(profile);
    }

    public async Task<PagedResult<CropCycleResponse>> SearchCropCyclesAsync(PagedQuery query, Guid? fieldId, CancellationToken cancellationToken)
    {
        query.Normalize();
        var cycles = ApplyCycleAccess(dbContext.CropCycles.AsNoTracking());
        if (fieldId.HasValue) cycles = cycles.Where(item => item.FieldId == fieldId.Value);
        cycles = query.SortDirection == "desc" ? cycles.OrderByDescending(item => item.PlannedStartDate) : cycles.OrderBy(item => item.PlannedStartDate);
        var total = await cycles.CountAsync(cancellationToken);
        var items = await cycles.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapCycle(item)).ToListAsync(cancellationToken);
        return new PagedResult<CropCycleResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<CropCycleResponse> CreateCropCycleAsync(CropCycleRequest request, CancellationToken cancellationToken)
    {
        Validate(cropCycleValidator.Validate(request));
        await EnsureFieldAccessAsync(request.FieldId, cancellationToken);
        await EnsureCropTypeAsync(request.CropTypeId, cancellationToken);
        var cycle = new CropCycle { FieldId = request.FieldId, CropTypeId = request.CropTypeId, PlannedStartDate = request.PlannedStartDate, PlannedEndDate = request.PlannedEndDate, Status = request.Status, CreatedByUserId = currentUser.UserId };
        dbContext.CropCycles.Add(cycle);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapCycle(cycle);
    }

    public async Task<PagedResult<CropPlanRequestResponse>> SearchCropPlanRequestsAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var requests = ApplyPlanRequestAccess(dbContext.CropPlanRequests.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            requests = requests.Where(item => item.Objective.ToLower().Contains(search));
        }

        requests = query.SortDirection == "desc" ? requests.OrderByDescending(item => item.CreatedAt) : requests.OrderBy(item => item.CreatedAt);
        var total = await requests.CountAsync(cancellationToken);
        var items = await requests.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(item => MapRequest(item)).ToListAsync(cancellationToken);
        return new PagedResult<CropPlanRequestResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<CropPlanRequestResponse> CreateCropPlanRequestAsync(CropPlanRequestCreate request, CancellationToken cancellationToken)
    {
        return await CreateRequestCoreAsync(request, CropPlanRequestStatus.Submitted, "Crop plan request submitted.", cancellationToken);
    }

    public async Task<CropPlanRequestResponse> GeneratePreliminaryRequestAsync(CropPlanRequestCreate request, CancellationToken cancellationToken)
    {
        return await CreateRequestCoreAsync(request, CropPlanRequestStatus.Submitted, "Crop plan request submitted for coordinator planning.", cancellationToken);
    }

    public async Task<CropPlanRequestResponse> UpdateCropPlanRequestAsync(Guid id, CropPlanRequestUpdate request, CancellationToken cancellationToken)
    {
        Validate(updateRequestValidator.Validate(request));
        var entity = await ApplyPlanRequestAccess(dbContext.CropPlanRequests).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Crop plan request");
        if (entity.Status != request.Status || entity.Status is CropPlanRequestStatus.Approved or CropPlanRequestStatus.Rejected or CropPlanRequestStatus.Cancelled)
            throw new ApiException(HttpStatusCode.BadRequest, "CROP_PLAN_STATUS_MANAGED", "Crop plan status is managed by the workflow and approval process.");
        if (await dbContext.AgentWorkflows.AnyAsync(item => item.CropPlanRequestId == id && !item.IsDeleted, cancellationToken))
            throw new ApiException(HttpStatusCode.Conflict, "CROP_PLAN_WORKFLOW_STARTED", "A crop plan with workflow history cannot be edited.");
        entity.PreferredStartDate = request.PreferredStartDate;
        entity.PreferredEndDate = request.PreferredEndDate;
        entity.Budget = request.Budget;
        entity.Objective = request.Objective.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRequest(entity);
    }

    public async Task<IReadOnlyList<CropPlanHistoryResponse>> GetCropPlanHistoryAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var exists = await ApplyPlanRequestAccess(dbContext.CropPlanRequests.AsNoTracking()).AnyAsync(item => item.Id == requestId, cancellationToken);
        if (!exists) throw NotFound("Crop plan request");
        return await dbContext.CropPlanRequestHistories.AsNoTracking()
            .Where(item => item.CropPlanRequestId == requestId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new CropPlanHistoryResponse(item.Id, item.CropPlanRequestId, item.FromStatus, item.ToStatus, item.Note, item.ChangedByUserId, item.CreatedAt))
            .ToListAsync(cancellationToken);
    }


    public async Task<CropPlanningWorkflowStartResponse> StartAiWorkflowAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await ApplyPlanRequestAccess(dbContext.CropPlanRequests)
            .Include(item => item.Farm)
            .Include(item => item.Field)
            .Include(item => item.CropType)
            .Include(item => item.CropVariety)
            .Include(item => item.PreviousCropType)
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw NotFound("Crop plan request");

        if (request.Status is CropPlanRequestStatus.Rejected or CropPlanRequestStatus.Cancelled or CropPlanRequestStatus.Approved)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "CROP_PLAN_STATE_NOT_ALLOWED", "Only submitted or preliminary crop plan requests can start AI planning.");
        }

        await EnsureCropTypeAsync(request.CropTypeId, cancellationToken);
        if (!request.FieldId.HasValue || !await ApplyFieldAccess(dbContext.Fields.AsNoTracking()).AnyAsync(item =>
                item.Id == request.FieldId.Value && item.FarmId == request.FarmId && item.IsActive, cancellationToken))
            throw new ApiException(HttpStatusCode.BadRequest, "FIELD_FARM_MISMATCH", "The selected active field must belong to the selected farm.");
        if (request.CropVarietyId.HasValue && (request.CropVariety is null || !request.CropVariety.IsActive || request.CropVariety.IsDeleted || request.CropVariety.CropTypeId != request.CropTypeId))
            throw new ApiException(HttpStatusCode.BadRequest, "INVALID_CROP_VARIETY", "Selected variety is not active for this crop.");

        var hasRunningWorkflow = await dbContext.AgentWorkflows.AsNoTracking().AnyAsync(workflow =>
            workflow.CropPlanRequestId == request.Id &&
            (workflow.Status == AgentWorkflowStatus.Pending || workflow.Status == AgentWorkflowStatus.Running),
            cancellationToken);
        if (hasRunningWorkflow)
        {
            throw new ApiException(HttpStatusCode.Conflict, "AI_WORKFLOW_ALREADY_ACTIVE", "An AI workflow is already active for this crop plan request.");
        }

        var userId = RequireUser();
        var cropCycleId = request.FieldId.HasValue
            ? await dbContext.CropCycles.AsNoTracking()
                .Where(cycle => cycle.FieldId == request.FieldId.Value && cycle.CropTypeId == request.CropTypeId && !cycle.IsDeleted)
                .OrderByDescending(cycle => cycle.CreatedAt)
                .Select(cycle => (Guid?)cycle.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var workflow = new AgentWorkflow
        {
            CropPlanRequestId = request.Id,
            InitiatedByUserId = userId,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Running,
            CurrentStep = CoordinatorAgentName,
            CreatedByUserId = userId
        };

        var input = new CropPlanningCoordinatorInput(
            workflow.Id,
            request.Id,
            request.RequestedByUserId,
            request.FarmId,
            request.FieldId,
            cropCycleId,
            request.CropTypeId,
            request.Objective,
            request.Budget,
            request.PreferredStartDate,
            request.CropVarietyId,
            request.CropVariety?.Name,
            request.CultivationSeason.ToString(),
            request.PreferredEndDate,
            request.PreviousCropTypeId,
            request.PreviousCropType?.Name,
            JsonSerializer.Deserialize<List<string>>(request.PreviousKnownProblemsJson, JsonOptions) ?? []);

        var step = new AgentStep
        {
            AgentWorkflowId = workflow.Id,
            AgentName = CoordinatorAgentName,
            StepName = CoordinatorStepName,
            Sequence = 1,
            InputJson = JsonSerializer.Serialize(input, JsonOptions),
            Status = AgentStepStatus.Running,
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        dbContext.AgentWorkflows.Add(workflow);
        dbContext.AgentSteps.Add(step);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var output = await agenticAIClient.RunCropPlanningCoordinatorAsync(input, cancellationToken);
            var validationErrors = ValidateCoordinatorOutput(workflow.Id, output);
            var isValid = validationErrors.Count == 0;

            step.OutputJson = isValid
                ? JsonSerializer.Serialize(output, JsonOptions)
                : JsonSerializer.Serialize(CreateSafeFailureOutput(workflow.Id, validationErrors), JsonOptions);
            step.Status = isValid && !output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase) ? AgentStepStatus.Completed : AgentStepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.ErrorCode = isValid ? (output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase) ? "AI_SAFE_FAILURE" : null) : "AI_RESPONSE_INVALID";
            step.ErrorMessageSafe = isValid ? (output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase) ? output.Warnings.FirstOrDefault() : null) : "The AI service returned a response that failed validation.";

            dbContext.AgentValidationResults.Add(new AgentValidationResult
            {
                AgentWorkflowId = workflow.Id,
                ValidatorName = "CropPlanningCoordinatorOutputValidator",
                IsValid = isValid,
                ErrorsJson = JsonSerializer.Serialize(validationErrors, JsonOptions),
                CreatedByUserId = userId
            });

            if (!isValid)
            {
                workflow.Status = AgentWorkflowStatus.Failed;
                workflow.CurrentStep = "SafeFailure";
                workflow.CompletedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return new CropPlanningWorkflowStartResponse(workflow.Id, request.Id, step.Id, "SafeFailure", true, validationErrors);
            }

            if (output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase))
            {
                workflow.Status = AgentWorkflowStatus.Failed;
                workflow.CurrentStep = "SafeFailure";
                workflow.CompletedAt = DateTime.UtcNow;
            }
            else if (output.Status.Equals("Planned", StringComparison.OrdinalIgnoreCase))
            {
                AddDownstreamSteps(workflow.Id, output.Steps ?? [], userId);
                workflow.Status = AgentWorkflowStatus.Pending;
                workflow.CurrentStep = "CropFieldAnalysisAgent";
                if (request.Status == CropPlanRequestStatus.Submitted)
                {
                    AddHistory(request.Id, request.Status, CropPlanRequestStatus.PreliminaryGenerated, "AI crop planning coordinator completed.");
                    request.Status = CropPlanRequestStatus.PreliminaryGenerated;
                }
            }
            else
            {
                workflow.Status = AgentWorkflowStatus.Pending;
                workflow.CurrentStep = output.RequiresHumanReview ? "HumanReview" : CoordinatorAgentName;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new CropPlanningWorkflowStartResponse(workflow.Id, request.Id, step.Id, output.Status, output.RequiresHumanReview, output.Warnings);
        }
        catch (Exception)
        {
            var warnings = new[] { "AI service is unavailable or timed out. No crop facts were generated." };
            var safeOutput = CreateSafeFailureOutput(workflow.Id, warnings);
            step.Status = AgentStepStatus.Failed;
            step.OutputJson = JsonSerializer.Serialize(safeOutput, JsonOptions);
            step.CompletedAt = DateTime.UtcNow;
            step.ErrorCode = "AI_SERVICE_UNAVAILABLE";
            step.ErrorMessageSafe = warnings[0];
            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.CurrentStep = "SafeFailure";
            workflow.CompletedAt = DateTime.UtcNow;
            dbContext.AgentValidationResults.Add(new AgentValidationResult
            {
                AgentWorkflowId = workflow.Id,
                ValidatorName = "CropPlanningCoordinatorAvailability",
                IsValid = false,
                ErrorsJson = JsonSerializer.Serialize(warnings, JsonOptions),
                CreatedByUserId = userId
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CropPlanningWorkflowStartResponse(workflow.Id, request.Id, step.Id, safeOutput.Status, true, warnings);
        }
    }

    public async Task<PrePlantingAssessmentResponse?> GetPrePlantingAssessmentAsync(Guid requestId, CancellationToken cancellationToken)
    {
        RequirePrePlantingViewer();
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var assessment = await dbContext.FieldInspections.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.CropPlanRequestId == requestId &&
                item.Purpose == InspectionPurpose.PrePlanting &&
                !item.IsDeleted,
                cancellationToken);

        return assessment is null
            ? null
            : await MapPrePlantingAssessmentAsync(assessment, cancellationToken);
    }

    public async Task<PrePlantingContextResponse> GetPrePlantingContextAsync(Guid requestId, CancellationToken cancellationToken)
    {
        RequirePrePlantingViewer();
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var planRequest = await dbContext.CropPlanRequests.AsNoTracking()
            .Include(item => item.RequestedByUser)
            .Include(item => item.Farm)
            .Include(item => item.Field)
            .Include(item => item.CropType)
            .Include(item => item.CropVariety)
            .SingleOrDefaultAsync(item => item.Id == requestId && !item.IsDeleted, cancellationToken)
            ?? throw NotFound("Crop plan request");
        var workflow = await LatestWorkflowQuery(requestId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");

        if (planRequest.FieldId is null || planRequest.Field is null)
            throw new ApiException(HttpStatusCode.BadRequest, "PREPLANT_FIELD_REQUIRED", "A field is required for pre-planting assessment context.");

        return new PrePlantingContextResponse(
            planRequest.Id,
            workflow.Id,
            workflow.CurrentStep,
            planRequest.RequestedByUserId,
            planRequest.RequestedByUser?.FullName ?? string.Empty,
            planRequest.FarmId,
            planRequest.Farm?.Name ?? string.Empty,
            planRequest.Farm?.Location ?? string.Empty,
            planRequest.FieldId.Value,
            planRequest.Field.Name,
            planRequest.CropTypeId,
            planRequest.CropType?.Name ?? string.Empty,
            planRequest.CropVarietyId,
            planRequest.CropVariety?.Name,
            planRequest.CultivationSeason,
            planRequest.PreferredStartDate,
            planRequest.PreferredEndDate);
    }

    public async Task<PrePlantingAssessmentResponse> SavePrePlantingAssessmentAsync(
        Guid requestId,
        PrePlantingAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        RequireFieldOfficer();
        Validate(prePlantingAssessmentValidator.Validate(request));
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);

        var planRequest = await dbContext.CropPlanRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId && !item.IsDeleted, cancellationToken)
            ?? throw NotFound("Crop plan request");
        if (!planRequest.FieldId.HasValue)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "PREPLANT_FIELD_REQUIRED", "A field is required before a pre-planting assessment can be saved.");
        }
        await EnsurePrePlantingFieldLinkageAsync(planRequest, cancellationToken);

        var actor = RequireUser();
        var assessment = await dbContext.FieldInspections
            .SingleOrDefaultAsync(item =>
                item.CropPlanRequestId == requestId &&
                item.Purpose == InspectionPurpose.PrePlanting &&
                !item.IsDeleted,
                cancellationToken);

        if (assessment?.Status == InspectionStatus.Completed)
        {
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_SUBMITTED", "A submitted pre-planting assessment cannot be edited.");
        }

        if (assessment is not null && assessment.InspectorUserId != actor)
        {
            throw new ApiException(HttpStatusCode.Forbidden, "PREPLANT_ASSESSMENT_OWNER_REQUIRED", "Only the Field Officer who created this assessment may change or submit it.");
        }

        if (assessment is not null && assessment.FieldId != planRequest.FieldId.Value)
        {
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_LINKAGE_INVALID", "The saved pre-planting assessment is not linked to the crop plan field.");
        }

        var workflow = await LatestWorkflowQuery(requestId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");
        if (workflow.CurrentStep != FieldAnalysisAgentName)
        {
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_STAGE_NOT_ACTIVE", "The workflow is not waiting for pre-planting field analysis.");
        }

        var isNewAssessment = assessment is null;
        if (assessment is null)
        {
            assessment = new FieldInspection
            {
                FieldId = planRequest.FieldId.Value,
                CropPlanRequestId = requestId,
                Purpose = InspectionPurpose.PrePlanting,
                InspectorUserId = actor,
                ScheduledAt = DateTime.UtcNow,
                Status = InspectionStatus.InProgress,
                Summary = BuildAssessmentSummary(request),
                CreatedByUserId = actor,
                UpdatedByUserId = actor
            };
            dbContext.FieldInspections.Add(assessment);
        }
        else
        {
            assessment.Summary = BuildAssessmentSummary(request);
            assessment.UpdatedAt = DateTime.UtcNow;
            assessment.UpdatedByUserId = actor;
        }

        var existingObservations = await dbContext.InspectionObservations
            .Where(item => item.FieldInspectionId == assessment.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        dbContext.InspectionObservations.RemoveRange(existingObservations);
        dbContext.InspectionObservations.AddRange(CreateAssessmentObservations(assessment, request, actor));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNewAssessment)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentAssessment = await dbContext.FieldInspections.AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.CropPlanRequestId == requestId
                    && item.Purpose == InspectionPurpose.PrePlanting
                    && !item.IsDeleted,
                    cancellationToken);
            if (concurrentAssessment is null) throw;
            if (concurrentAssessment.InspectorUserId != actor)
                throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_ALREADY_EXISTS", "A pre-planting assessment already exists for this crop plan request.");
            return await MapPrePlantingAssessmentAsync(concurrentAssessment, cancellationToken);
        }

        return await MapPrePlantingAssessmentAsync(assessment, cancellationToken);
    }

    public async Task<PrePlantingAssessmentResponse> SubmitPrePlantingAssessmentAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        RequireFieldOfficer();
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var planRequest = await dbContext.CropPlanRequests.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId && !item.IsDeleted, cancellationToken)
            ?? throw NotFound("Crop plan request");
        if (!planRequest.FieldId.HasValue)
            throw new ApiException(HttpStatusCode.BadRequest, "PREPLANT_FIELD_REQUIRED", "A field is required before a pre-planting assessment can be submitted.");
        await EnsurePrePlantingFieldLinkageAsync(planRequest, cancellationToken);

        var workflow = await LatestWorkflowQuery(requestId)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");

        var assessment = await dbContext.FieldInspections
            .SingleOrDefaultAsync(item =>
                item.CropPlanRequestId == requestId
                && item.Purpose == InspectionPurpose.PrePlanting
                && !item.IsDeleted,
                cancellationToken)
            ?? throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_REQUIRED", "A linked pre-planting assessment must be saved before submission.");
        var actor = RequireUser();
        if (assessment.InspectorUserId != actor)
            throw new ApiException(HttpStatusCode.Forbidden, "PREPLANT_ASSESSMENT_OWNER_REQUIRED", "Only the Field Officer who created this assessment may change or submit it.");
        if (assessment.CropPlanRequestId != requestId
            || assessment.FieldId != planRequest.FieldId.Value
            || assessment.Purpose != InspectionPurpose.PrePlanting)
        {
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_LINKAGE_INVALID", "The pre-planting assessment does not match the crop plan request and field.");
        }
        if (assessment.Status is not (InspectionStatus.InProgress or InspectionStatus.Completed))
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_STATE_INVALID", "Only an in-progress pre-planting assessment can be submitted.");

        var observations = await dbContext.InspectionObservations.AsNoTracking()
            .Where(item => item.FieldInspectionId == assessment.Id && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var images = await dbContext.InspectionImages.AsNoTracking()
            .Where(item => item.FieldInspectionId == assessment.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (images.Any(image => image.FieldInspectionId != assessment.Id))
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_EVIDENCE_LINKAGE_INVALID", "Assessment evidence is not linked to the exact pre-planting inspection.");

        var persistenceErrors = new List<string>();
        var persistedRequest = ReadAssessmentRequest(observations, persistenceErrors);
        persistenceErrors.AddRange(PrePlantingAssessmentRules.ValidateSubmission(persistedRequest));
        if (persistenceErrors.Count > 0)
        {
            throw new ApiException(
                HttpStatusCode.BadRequest,
                "PREPLANT_ASSESSMENT_INVALID",
                string.Join(" ", persistenceErrors.Distinct(StringComparer.Ordinal)));
        }

        if (assessment.Status == InspectionStatus.InProgress)
        {
            if (workflow.CurrentStep != FieldAnalysisAgentName)
                throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_STAGE_NOT_ACTIVE", "The workflow is not waiting for pre-planting field analysis.");
            assessment.Status = InspectionStatus.Completed;
            assessment.CompletedAt = DateTime.UtcNow;
            assessment.UpdatedAt = DateTime.UtcNow;
            assessment.UpdatedByUserId = actor;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await MapPrePlantingAssessmentAsync(assessment, cancellationToken);
    }

    public async Task<FieldAnalysisRunResponse> RunFieldAnalysisAsync(Guid requestId, CancellationToken cancellationToken)
    {
        RequireFieldOfficer();
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var workflow = await LatestWorkflowQuery(requestId)
            .Include(item => item.CropPlanRequest)!
                .ThenInclude(request => request!.Farm)
            .Include(item => item.CropPlanRequest)!
                .ThenInclude(request => request!.Field)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");

        var planRequest = workflow.CropPlanRequest ?? throw NotFound("Crop plan request");
        if (!planRequest.FieldId.HasValue)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "FIELD_ANALYSIS_REQUIRES_FIELD", "Field analysis requires a crop plan request with a field.");
        }

        var step = workflow.Steps
            .OrderBy(item => item.Sequence)
            .FirstOrDefault(item => item.AgentName == FieldAnalysisAgentName && item.StepName == FieldAnalysisStepName)
            ?? throw NotFound("Field analysis step");

        if (step.Status == AgentStepStatus.Completed)
        {
            var completedOutput = ReadFieldAnalysisOutput(step.OutputJson, workflow.Id, ["Field analysis was already completed."]);
            return new FieldAnalysisRunResponse(workflow.Id, planRequest.Id, step.Id, completedOutput.Status, completedOutput.RequiresHumanReview, completedOutput.Warnings);
        }

        if (step.Status == AgentStepStatus.Running)
        {
            throw new ApiException(HttpStatusCode.Conflict, "FIELD_ANALYSIS_ALREADY_RUNNING", "Field analysis is already running for this workflow.");
        }

        var assessment = await dbContext.FieldInspections.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.CropPlanRequestId == requestId &&
                item.Purpose == InspectionPurpose.PrePlanting &&
                !item.IsDeleted,
                cancellationToken);
        if (assessment is null)
        {
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_REQUIRED", "A linked pre-planting assessment must be saved before field analysis can run.");
        }
        if (assessment.FieldId != planRequest.FieldId.Value || assessment.Status != InspectionStatus.Completed)
        {
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_ASSESSMENT_NOT_SUBMITTED", "The linked pre-planting assessment must be submitted before field analysis can run.");
        }

        var cropCycleId = await dbContext.CropCycles.AsNoTracking()
            .Where(cycle => cycle.FieldId == planRequest.FieldId.Value && cycle.CropTypeId == planRequest.CropTypeId && !cycle.IsDeleted)
            .OrderByDescending(cycle => cycle.CreatedAt)
            .Select(cycle => (Guid?)cycle.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var cropReferenceProfileId = await dbContext.CropReferenceProfiles.AsNoTracking()
            .Where(profile => profile.CropTypeId == planRequest.CropTypeId && profile.IsActive && !profile.IsDeleted)
            .OrderByDescending(profile => profile.VerifiedAt)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var input = new FieldAnalysisInput(
            workflow.Id,
            planRequest.Id,
            assessment.Id,
            planRequest.FieldId.Value,
            cropCycleId,
            ["FieldCondition", "OpenIssues", "InspectionEvidence"],
            cropReferenceProfileId,
            step.Id);

        var userId = RequireUser();
        step.InputJson = JsonSerializer.Serialize(input, JsonOptions);
        step.Status = AgentStepStatus.Running;
        step.StartedAt = DateTime.UtcNow;
        step.ErrorCode = null;
        step.ErrorMessageSafe = null;
        workflow.Status = AgentWorkflowStatus.Running;
        workflow.CurrentStep = FieldAnalysisAgentName;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var output = await agenticAIClient.RunFieldAnalysisAsync(input, cancellationToken);
            var validationErrors = await ValidateFieldAnalysisOutputAsync(workflow.Id, assessment.Id, output, cancellationToken);
            var isValid = validationErrors.Count == 0;
            var safeOutput = isValid ? output : CreateFieldAnalysisSafeFailureOutput(workflow.Id, validationErrors);

            step.OutputJson = JsonSerializer.Serialize(safeOutput, JsonOptions);
            step.Status = isValid && !safeOutput.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase) ? AgentStepStatus.Completed : AgentStepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.ErrorCode = isValid ? (safeOutput.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase) ? "AI_SAFE_FAILURE" : null) : "AI_RESPONSE_INVALID";
            step.ErrorMessageSafe = isValid ? (safeOutput.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase) ? safeOutput.Warnings.FirstOrDefault() : null) : "The field-analysis response failed validation.";

            dbContext.AgentValidationResults.Add(new AgentValidationResult
            {
                AgentWorkflowId = workflow.Id,
                ValidatorName = "CropFieldAnalysisOutputValidator",
                IsValid = isValid,
                ErrorsJson = JsonSerializer.Serialize(validationErrors, JsonOptions),
                CreatedByUserId = userId
            });

            if (step.Status == AgentStepStatus.Completed)
            {
                workflow.Status = AgentWorkflowStatus.Pending;
                workflow.CurrentStep = WeatherResourceAgentName;
            }
            else
            {
                workflow.Status = AgentWorkflowStatus.Failed;
                workflow.CurrentStep = "SafeFailure";
                workflow.CompletedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new FieldAnalysisRunResponse(workflow.Id, planRequest.Id, step.Id, safeOutput.Status, safeOutput.RequiresHumanReview, safeOutput.Warnings);
        }
        catch (Exception)
        {
            var warnings = new[] { "AI service is unavailable or timed out during field analysis. No field conclusions were generated." };
            var safeOutput = CreateFieldAnalysisSafeFailureOutput(workflow.Id, warnings);
            step.Status = AgentStepStatus.Failed;
            step.OutputJson = JsonSerializer.Serialize(safeOutput, JsonOptions);
            step.CompletedAt = DateTime.UtcNow;
            step.ErrorCode = "AI_SERVICE_UNAVAILABLE";
            step.ErrorMessageSafe = warnings[0];
            workflow.Status = AgentWorkflowStatus.Failed;
            workflow.CurrentStep = "SafeFailure";
            workflow.CompletedAt = DateTime.UtcNow;
            dbContext.AgentValidationResults.Add(new AgentValidationResult
            {
                AgentWorkflowId = workflow.Id,
                ValidatorName = "CropFieldAnalysisAvailability",
                IsValid = false,
                ErrorsJson = JsonSerializer.Serialize(warnings, JsonOptions),
                CreatedByUserId = userId
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return new FieldAnalysisRunResponse(workflow.Id, planRequest.Id, step.Id, safeOutput.Status, true, warnings);
        }
    }
    public async Task<CropPlanningWorkflowStatusResponse> GetWorkflowStatusAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var workflow = await LatestWorkflowQuery(requestId)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");

        var warnings = ExtractWarnings(workflow.Steps.FirstOrDefault(step => step.StepName == CoordinatorStepName)?.OutputJson);
        var steps = workflow.Steps
            .OrderBy(step => step.Sequence)
            .Select(step => new AgentStepStatusResponse(step.Id, step.AgentName, step.StepName, step.Sequence, step.Status, step.StartedAt, step.CompletedAt, step.ErrorCode, step.ErrorMessageSafe))
            .ToList();

        return new CropPlanningWorkflowStatusResponse(workflow.Id, requestId, workflow.Status, workflow.CurrentStep, workflow.CreatedAt, workflow.CompletedAt, steps, warnings);
    }

    public async Task<CropPlanningResultResponse> GetPlanningResultAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var workflow = await LatestWorkflowQuery(requestId)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");
        var coordinatorOutput = workflow.Steps
            .OrderBy(step => step.Sequence)
            .FirstOrDefault(step => step.StepName == CoordinatorStepName)?.OutputJson;

        if (string.IsNullOrWhiteSpace(coordinatorOutput))
        {
            return CreateResultResponse(workflow.Id, CreateSafeFailureOutput(workflow.Id, new[] { "Coordinator output is not available yet." }));
        }

        try
        {
            var output = JsonSerializer.Deserialize<CropPlanningCoordinatorOutput>(coordinatorOutput, JsonOptions)
                ?? CreateSafeFailureOutput(workflow.Id, new[] { "Coordinator output is empty." });
            return CreateResultResponse(workflow.Id, output);
        }
        catch (JsonException)
        {
            return CreateResultResponse(workflow.Id, CreateSafeFailureOutput(workflow.Id, new[] { "Coordinator output could not be read safely." }));
        }
    }

    public async Task<FieldAnalysisOutput> GetFieldAnalysisResultAsync(Guid requestId, CancellationToken cancellationToken)
    {
        RequirePrePlantingViewer();
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var workflow = await LatestWorkflowQuery(requestId)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");

        var outputJson = workflow.Steps
            .OrderBy(step => step.Sequence)
            .FirstOrDefault(step => step.AgentName == FieldAnalysisAgentName && step.StepName == FieldAnalysisStepName)?.OutputJson;

        return ReadFieldAnalysisOutput(outputJson, workflow.Id, ["Field analysis output is not available yet."]);
    }

    public async Task<Member3HandoffResponse> GetMember3HandoffAsync(Guid requestId, CancellationToken cancellationToken)
    {
        RequirePrePlantingViewer();
        await EnsurePlanRequestAccessAsync(requestId, cancellationToken);
        var workflow = await LatestWorkflowQuery(requestId)
            .Include(item => item.CropPlanRequest)!
                .ThenInclude(request => request!.Farm)
            .Include(item => item.CropPlanRequest)!
                .ThenInclude(request => request!.Field)
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("AI workflow");

        var planRequest = workflow.CropPlanRequest ?? throw NotFound("Crop plan request");
        var fieldAnalysis = ReadFieldAnalysisOutput(
            workflow.Steps.OrderBy(step => step.Sequence).FirstOrDefault(step => step.AgentName == FieldAnalysisAgentName && step.StepName == FieldAnalysisStepName)?.OutputJson,
            workflow.Id,
            ["Field analysis output is not available yet."]);

        var cropCycleId = planRequest.FieldId.HasValue
            ? await dbContext.CropCycles.AsNoTracking()
                .Where(cycle => cycle.FieldId == planRequest.FieldId.Value && cycle.CropTypeId == planRequest.CropTypeId && !cycle.IsDeleted)
                .OrderByDescending(cycle => cycle.CreatedAt)
                .Select(cycle => (Guid?)cycle.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var fieldLocation = planRequest.Field is null
            ? planRequest.Farm?.Location ?? string.Empty
            : $"{planRequest.Field.Name} at {planRequest.Farm?.Location ?? "unknown location"}";

        return new Member3HandoffResponse(
            workflow.Id,
            planRequest.Id,
            planRequest.FieldId,
            cropCycleId,
            fieldLocation,
            planRequest.PreferredStartDate,
            planRequest.PreferredEndDate,
            fieldAnalysis.FieldCondition.Summary,
            fieldAnalysis.Priority,
            fieldAnalysis.FieldCondition.EvidenceInspectionIds,
            fieldAnalysis.OpenIssues,
            fieldAnalysis.Warnings);
    }
    private async Task<CropPlanRequestResponse> CreateRequestCoreAsync(CropPlanRequestCreate request, CropPlanRequestStatus status, string note, CancellationToken cancellationToken)
    {
        Validate(createRequestValidator.Validate(request));
        var userId = RequireUser();
        await EnsureFarmAccessAsync(request.FarmId, cancellationToken);
        var fieldValid = await ApplyFieldAccess(dbContext.Fields.AsNoTracking()).AnyAsync(item =>
            item.Id == request.FieldId!.Value && item.FarmId == request.FarmId && item.IsActive, cancellationToken);
        if (!fieldValid) throw new ApiException(HttpStatusCode.BadRequest, "FIELD_FARM_MISMATCH", "The selected active field must belong to the selected farm.");
        await EnsureCropTypeAsync(request.CropTypeId, cancellationToken);
        if (request.CropVarietyId.HasValue && !await dbContext.CropVarieties.AsNoTracking().AnyAsync(item =>
                item.Id == request.CropVarietyId.Value && item.CropTypeId == request.CropTypeId && item.IsActive && !item.IsDeleted, cancellationToken))
            throw new ApiException(HttpStatusCode.BadRequest, "INVALID_CROP_VARIETY", "Selected variety is not active for this crop.");
        if (request.PreviousCropTypeId.HasValue) await EnsureCropTypeAsync(request.PreviousCropTypeId.Value, cancellationToken);

        var activeStatuses = new[] { CropPlanRequestStatus.Submitted, CropPlanRequestStatus.PreliminaryGenerated, CropPlanRequestStatus.Approved };
        var duplicate = await dbContext.CropPlanRequests.AnyAsync(item =>
            item.FarmId == request.FarmId &&
            item.FieldId == request.FieldId &&
            item.CropTypeId == request.CropTypeId &&
            activeStatuses.Contains(item.Status) &&
            !item.IsDeleted,
            cancellationToken);
        if (duplicate) throw new ApiException(HttpStatusCode.Conflict, "DUPLICATE_ACTIVE_CROP_REQUEST", "An active crop planning request already exists for this farm, field, and crop type.");

        var entity = new CropPlanRequest
        {
            FarmId = request.FarmId,
            FieldId = request.FieldId,
            CropTypeId = request.CropTypeId,
            CropVarietyId = request.CropVarietyId,
            CultivationSeason = request.CultivationSeason,
            PreviousCropTypeId = request.PreviousCropTypeId,
            PreviousKnownProblemsJson = JsonSerializer.Serialize(request.PreviousKnownProblems ?? [], JsonOptions),
            RequestedByUserId = userId,
            PreferredStartDate = request.PreferredStartDate,
            PreferredEndDate = request.PreferredEndDate,
            Budget = request.Budget,
            Objective = request.Objective.Trim(),
            Status = status,
            CreatedByUserId = userId
        };
        dbContext.CropPlanRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        AddHistory(entity.Id, CropPlanRequestStatus.Draft, status, note);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRequest(entity);
    }

    private IQueryable<Farm> ApplyFarmAccess(IQueryable<Farm> query)
    {
        query = query.Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.OwnerUserId == currentUser.UserId) : query;
    }

    private IQueryable<Field> ApplyFieldAccess(IQueryable<Field> query)
    {
        query = query.Include(item => item.Farm).Where(item => !item.IsDeleted && item.Farm != null && !item.Farm.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.Farm!.OwnerUserId == currentUser.UserId) : query;
    }

    private IQueryable<CropCycle> ApplyCycleAccess(IQueryable<CropCycle> query)
    {
        query = query.Include(item => item.Field)!.ThenInclude(field => field!.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.Field!.Farm!.OwnerUserId == currentUser.UserId) : query;
    }

    private IQueryable<CropPlanRequest> ApplyPlanRequestAccess(IQueryable<CropPlanRequest> query)
    {
        query = query.Include(item => item.Farm).Where(item => !item.IsDeleted);
        return currentUser.Role == ApplicationRole.Farmer ? query.Where(item => item.Farm!.OwnerUserId == currentUser.UserId) : query;
    }

    private async Task EnsureFarmAccessAsync(Guid farmId, CancellationToken cancellationToken)
    {
        if (!await ApplyFarmAccess(dbContext.Farms.AsNoTracking()).AnyAsync(item => item.Id == farmId, cancellationToken)) throw NotFound("Farm");
    }

    private bool UsesPostgreSql => string.Equals(
        dbContext.Database.ProviderName,
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        StringComparison.Ordinal);

    private async Task<IDbContextTransaction?> BeginCapacityTransactionAsync(CancellationToken cancellationToken) =>
        UsesPostgreSql
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

    private async Task<Farm> LockFarmForCapacityAsync(Guid farmId, CancellationToken cancellationToken)
    {
        var farms = UsesPostgreSql
            ? dbContext.Farms.FromSqlRaw(
                "SELECT * FROM \"Farms\" WHERE \"Id\" = {0} FOR UPDATE",
                farmId)
            : dbContext.Farms;

        return await ApplyFarmAccess(farms).SingleOrDefaultAsync(cancellationToken) ?? throw NotFound("Farm");
    }

    private async Task<decimal> GetAllocatedFieldAreaAsync(
        Guid farmId,
        Guid? excludedFieldId = null,
        CancellationToken cancellationToken = default)
    {
        var activeFields = dbContext.Fields.AsNoTracking().Where(field =>
            field.FarmId == farmId &&
            field.IsActive &&
            !field.IsDeleted);
        if (excludedFieldId.HasValue)
        {
            activeFields = activeFields.Where(field => field.Id != excludedFieldId.Value);
        }

        return await activeFields.SumAsync(field => field.Area, cancellationToken);
    }

    private async Task EnsureFieldAccessAsync(Guid fieldId, CancellationToken cancellationToken)
    {
        if (!await ApplyFieldAccess(dbContext.Fields.AsNoTracking()).AnyAsync(item => item.Id == fieldId, cancellationToken)) throw NotFound("Field");
    }

    private async Task EnsureCropTypeAsync(Guid cropTypeId, CancellationToken cancellationToken)
    {
        if (!await dbContext.CropTypes.AsNoTracking().AnyAsync(item => item.Id == cropTypeId && item.IsActive && !item.IsDeleted, cancellationToken)) throw NotFound("Crop type");
    }


    private async Task EnsurePlanRequestAccessAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (!await ApplyPlanRequestAccess(dbContext.CropPlanRequests.AsNoTracking()).AnyAsync(item => item.Id == requestId, cancellationToken)) throw NotFound("Crop plan request");
    }

    private async Task EnsurePrePlantingFieldLinkageAsync(CropPlanRequest planRequest, CancellationToken cancellationToken)
    {
        if (!planRequest.FieldId.HasValue
            || !await dbContext.Fields.AsNoTracking().AnyAsync(field =>
                field.Id == planRequest.FieldId.Value
                && field.FarmId == planRequest.FarmId
                && field.IsActive
                && !field.IsDeleted,
                cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, "PREPLANT_FIELD_LINKAGE_INVALID", "The crop plan request must reference an active field on the same farm.");
        }
    }

    private IQueryable<AgentWorkflow> LatestWorkflowQuery(Guid requestId) =>
        dbContext.AgentWorkflows
            .Where(workflow => workflow.CropPlanRequestId == requestId && !workflow.IsDeleted)
            .OrderByDescending(workflow => workflow.CreatedAt)
            .ThenByDescending(workflow => workflow.Id)
            .Take(1);

    private void AddDownstreamSteps(Guid workflowId, IReadOnlyList<CropPlanningDelegatedStepResponse> steps, Guid userId)
    {
        foreach (var delegatedStep in steps.OrderBy(item => item.Sequence))
        {
            dbContext.AgentSteps.Add(new AgentStep
            {
                AgentWorkflowId = workflowId,
                AgentName = delegatedStep.AssignedAgent,
                StepName = delegatedStep.StepType,
                Sequence = delegatedStep.Sequence + 1,
                InputJson = JsonSerializer.Serialize(new { workflowId, delegatedStep.StepType }, JsonOptions),
                Status = AgentStepStatus.Pending,
                CreatedByUserId = userId
            });
        }
    }


    private async Task<PrePlantingAssessmentResponse> MapPrePlantingAssessmentAsync(
        FieldInspection assessment,
        CancellationToken cancellationToken)
    {
        var observations = await dbContext.InspectionObservations.AsNoTracking()
            .Where(item => item.FieldInspectionId == assessment.Id && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var request = ReadAssessmentRequest(observations);
        var images = await dbContext.InspectionImages.AsNoTracking()
            .Where(item => item.FieldInspectionId == assessment.Id && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new PrePlantingAssessmentImageResponse(item.Id, item.Url, item.ContentType, item.SizeBytes))
            .ToListAsync(cancellationToken);

        return new PrePlantingAssessmentResponse
        {
            InspectionId = assessment.Id,
            CropPlanRequestId = assessment.CropPlanRequestId!.Value,
            FieldId = assessment.FieldId,
            InspectorUserId = assessment.InspectorUserId,
            Status = assessment.Status,
            ScheduledAt = assessment.ScheduledAt,
            CompletedAt = assessment.CompletedAt,
            SoilType = request.SoilType,
            SoilCondition = request.SoilCondition,
            SoilMoisture = request.SoilMoisture,
            SoilNotes = request.SoilNotes,
            WaterAvailability = request.WaterAvailability,
            MainWaterSource = request.MainWaterSource,
            IrrigationAvailability = request.IrrigationAvailability,
            WaterReliability = request.WaterReliability,
            WaterConcerns = request.WaterConcerns,
            DrainageCondition = request.DrainageCondition,
            WaterloggingRisk = request.WaterloggingRisk,
            DrainageNotes = request.DrainageNotes,
            GeneralFieldCondition = request.GeneralFieldCondition,
            GeneralFieldNotes = request.GeneralFieldNotes,
            PlantingReadiness = request.PlantingReadiness,
            IdentifiedRisks = request.IdentifiedRisks,
            RiskNotes = request.RiskNotes,
            RisksAndConcerns = request.RiskNotes,
            OfficerNotes = request.OfficerNotes,
            Images = images
        };
    }

    private static PrePlantingAssessmentRequest ReadAssessmentRequest(
        IReadOnlyList<InspectionObservation> observations,
        List<string>? errors = null)
    {
        string? Value(string observationType)
        {
            var matches = observations.Where(item => item.ObservationType == observationType).ToArray();
            if (matches.Length > 1)
                errors?.Add($"Assessment contains duplicate {observationType} observations.");
            return matches.LastOrDefault()?.Notes;
        }

        TEnum? EnumValue<TEnum>(string observationType)
            where TEnum : struct, Enum
        {
            var value = Value(observationType);
            if (value is null) return null;
            if (Enum.TryParse<TEnum>(value, false, out var parsed) && Enum.IsDefined(parsed)) return parsed;
            errors?.Add($"Assessment contains an invalid {observationType} value.");
            return null;
        }

        var markers = observations.Where(item => item.ObservationType == RiskAssessmentObservationType).ToArray();
        if (markers.Length > 1)
            errors?.Add("Assessment contains duplicate identified-risk state markers.");
        var risksAssessed = markers.Length > 0;
        if (markers.Any(marker => marker.Notes != RiskAssessmentCompleteValue))
            errors?.Add("Assessment contains an invalid identified-risk state marker.");

        var riskRows = observations.Where(item => item.ObservationType == RiskObservationType).ToArray();
        if (!risksAssessed && riskRows.Length > 0)
            errors?.Add("Assessment contains identified risks without an assessed-risk marker.");
        IReadOnlyList<PrePlantingRisk>? identifiedRisks = null;
        if (risksAssessed)
        {
            var parsedRisks = new List<PrePlantingRisk>();
            foreach (var riskRow in riskRows)
            {
                if (Enum.TryParse<PrePlantingRisk>(riskRow.Notes, false, out var risk) && Enum.IsDefined(risk))
                    parsedRisks.Add(risk);
                else
                    errors?.Add("Assessment contains an invalid identified risk value.");
            }
            identifiedRisks = parsedRisks;
        }

        var legacyRiskNotes = Value("RisksAndConcerns");
        return new PrePlantingAssessmentRequest
        {
            SoilType = EnumValue<PrePlantingSoilType>("SoilType"),
            SoilCondition = EnumValue<PrePlantingSoilCondition>("SoilCondition"),
            SoilMoisture = EnumValue<PrePlantingSoilMoisture>("SoilMoisture"),
            SoilNotes = Value("SoilNotes"),
            WaterAvailability = EnumValue<PrePlantingWaterAvailability>("WaterAvailability"),
            MainWaterSource = Value("MainWaterSource"),
            IrrigationAvailability = EnumValue<PrePlantingIrrigationAvailability>("IrrigationAvailability"),
            WaterReliability = EnumValue<PrePlantingWaterReliability>("WaterReliability"),
            WaterConcerns = Value("WaterConcerns"),
            DrainageCondition = EnumValue<PrePlantingDrainageCondition>("DrainageCondition"),
            WaterloggingRisk = EnumValue<PrePlantingWaterloggingRisk>("WaterloggingRisk"),
            DrainageNotes = Value("DrainageNotes"),
            GeneralFieldCondition = EnumValue<PrePlantingGeneralFieldCondition>("GeneralFieldCondition"),
            GeneralFieldNotes = Value("GeneralFieldNotes"),
            PlantingReadiness = EnumValue<PrePlantingPlantingReadiness>("PlantingReadiness"),
            IdentifiedRisks = identifiedRisks,
            RiskNotes = Value("RiskNotes") ?? legacyRiskNotes,
            RisksAndConcerns = legacyRiskNotes,
            OfficerNotes = Value("OfficerNotes")
        };
    }

    private static IReadOnlyList<InspectionObservation> CreateAssessmentObservations(
        FieldInspection assessment,
        PrePlantingAssessmentRequest request,
        Guid actor)
    {
        var observations = new List<InspectionObservation>();

        void AddValue(string observationType, string? value)
        {
            if (value is null) return;
            observations.Add(new InspectionObservation
            {
                FieldInspection = assessment,
                ObservationType = observationType,
                Notes = value.Trim(),
                CreatedByUserId = actor,
                UpdatedByUserId = actor
            });
        }

        void AddEnum<TEnum>(string observationType, TEnum? value)
            where TEnum : struct, Enum => AddValue(observationType, value?.ToString());

        AddEnum("SoilType", request.SoilType);
        AddEnum("SoilCondition", request.SoilCondition);
        AddEnum("SoilMoisture", request.SoilMoisture);
        AddValue("SoilNotes", request.SoilNotes);
        AddEnum("WaterAvailability", request.WaterAvailability);
        AddValue("MainWaterSource", request.MainWaterSource);
        AddEnum("IrrigationAvailability", request.IrrigationAvailability);
        AddEnum("WaterReliability", request.WaterReliability);
        AddValue("WaterConcerns", request.WaterConcerns);
        AddEnum("DrainageCondition", request.DrainageCondition);
        AddEnum("WaterloggingRisk", request.WaterloggingRisk);
        AddValue("DrainageNotes", request.DrainageNotes);
        AddEnum("GeneralFieldCondition", request.GeneralFieldCondition);
        AddValue("GeneralFieldNotes", request.GeneralFieldNotes);
        AddEnum("PlantingReadiness", request.PlantingReadiness);
        AddValue("RiskNotes", request.RiskNotes ?? request.RisksAndConcerns);
        AddValue("OfficerNotes", request.OfficerNotes);

        if (request.IdentifiedRisks is not null)
        {
            AddValue(RiskAssessmentObservationType, RiskAssessmentCompleteValue);
            foreach (var risk in request.IdentifiedRisks)
                AddValue(RiskObservationType, risk.ToString());
        }

        return observations;
    }

    private static string BuildAssessmentSummary(PrePlantingAssessmentRequest request) =>
        (request.GeneralFieldNotes
            ?? request.GeneralFieldCondition?.ToString()
            ?? "Pre-planting assessment draft").Trim();

    private async Task<IReadOnlyList<string>> ValidateFieldAnalysisOutputAsync(Guid workflowId, Guid assessmentId, FieldAnalysisOutput output, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (output.WorkflowId != workflowId) errors.Add("FieldAnalysis workflowId does not match the persisted workflow.");
        if (string.IsNullOrWhiteSpace(output.Status)) errors.Add("FieldAnalysis status is required.");
        if (output.Warnings is null) errors.Add("FieldAnalysis warnings array is required.");
        if (output.FieldCondition is null) errors.Add("FieldAnalysis fieldCondition is required.");
        if (output.OpenIssues is null) errors.Add("FieldAnalysis openIssues array is required.");
        if (output.FieldCondition is null || output.OpenIssues is null) return errors;

        if (output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase))
        {
            if (!output.RequiresHumanReview) errors.Add("FieldAnalysis safe failures must require human review.");
            return errors;
        }

        if (!output.Status.Equals("Analyzed", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("FieldAnalysis status must be Analyzed or SafeFailure.");
        }

        foreach (var inspectionId in output.FieldCondition.EvidenceInspectionIds)
        {
            if (inspectionId != assessmentId) errors.Add($"FieldAnalysis referenced inspection evidence outside the linked pre-planting assessment: {inspectionId}.");
        }

        var knownIssueIds = await dbContext.CropIssues.AsNoTracking()
            .Where(issue => issue.FieldInspectionId == assessmentId && !issue.IsDeleted)
            .Select(issue => issue.Id)
            .ToListAsync(cancellationToken);
        foreach (var issue in output.OpenIssues)
        {
            if (!knownIssueIds.Contains(issue.IssueId)) errors.Add($"FieldAnalysis referenced unknown crop issue ID {issue.IssueId}.");
        }

        return errors;
    }
    private static IReadOnlyList<string> ValidateCoordinatorOutput(Guid workflowId, CropPlanningCoordinatorOutput output)
    {
        var errors = new List<string>();
        if (output.WorkflowId != workflowId) errors.Add("Coordinator workflowId does not match the persisted workflow.");
        if (string.IsNullOrWhiteSpace(output.Status)) errors.Add("Coordinator status is required.");
        if (output.Warnings is null) errors.Add("Coordinator warnings array is required.");

        if (output.Status.Equals("SafeFailure", StringComparison.OrdinalIgnoreCase))
        {
            if (!output.RequiresHumanReview) errors.Add("Safe failures must require human review.");
            return errors;
        }

        if (output.Status.Equals("ReferenceDataUnavailable", StringComparison.OrdinalIgnoreCase))
        {
            if (!output.RequiresHumanReview) errors.Add("Missing reference data must require human review.");
            return errors;
        }

        if (!output.Status.Equals("Planned", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Coordinator status must be Planned, ReferenceDataUnavailable, or SafeFailure.");
        }

        var expectedSteps = new[]
        {
            ("FieldAnalysis", "CropFieldAnalysisAgent"),
            ("WeatherResourceAnalysis", "WeatherResourceAgent"),
            ("Scheduling", "SchedulingValidationAgent")
        };
        var steps = output.Steps ?? [];
        if (steps.Count != expectedSteps.Length)
        {
            errors.Add("Coordinator must delegate exactly three downstream analysis steps.");
            return errors;
        }

        for (var index = 0; index < expectedSteps.Length; index++)
        {
            var expected = expectedSteps[index];
            var actual = steps[index];
            if (actual.Sequence != index + 1 || actual.StepType != expected.Item1 || actual.AssignedAgent != expected.Item2)
            {
                errors.Add($"Coordinator step {index + 1} must delegate {expected.Item1} to {expected.Item2}.");
            }
        }

        return errors;
    }


    private static FieldAnalysisOutput CreateFieldAnalysisSafeFailureOutput(Guid workflowId, IEnumerable<string> warnings) =>
        new(workflowId, "SafeFailure", true, warnings.ToArray(), new FieldAnalysisFieldConditionResponse(string.Empty, []), [], "Unknown");

    private static FieldAnalysisOutput ReadFieldAnalysisOutput(string? outputJson, Guid workflowId, IEnumerable<string> emptyWarnings)
    {
        if (string.IsNullOrWhiteSpace(outputJson)) return CreateFieldAnalysisSafeFailureOutput(workflowId, emptyWarnings);
        try
        {
            return JsonSerializer.Deserialize<FieldAnalysisOutput>(outputJson, JsonOptions)
                ?? CreateFieldAnalysisSafeFailureOutput(workflowId, ["Field analysis output is empty."]);
        }
        catch (JsonException)
        {
            return CreateFieldAnalysisSafeFailureOutput(workflowId, ["Field analysis output could not be read safely."]);
        }
    }
    private static CropPlanningCoordinatorOutput CreateSafeFailureOutput(Guid workflowId, IEnumerable<string> warnings) =>
        new(workflowId, "SafeFailure", true, warnings.ToArray(), "Unknown", string.Empty, []);

    private static CropPlanningResultResponse CreateResultResponse(Guid workflowId, CropPlanningCoordinatorOutput output) =>
        new(
            workflowId,
            output.Status,
            output.RequiresHumanReview,
            output.Warnings,
            output.ReferenceDataStatus ?? "Unknown",
            output.ObjectiveSummary ?? string.Empty,
            output.Steps ?? []);

    private static IReadOnlyList<string> ExtractWarnings(string? outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson)) return [];
        try
        {
            using var document = JsonDocument.Parse(outputJson);
            if (!document.RootElement.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array) return [];
            return warnings.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }
        catch (JsonException)
        {
            return ["Stored coordinator warnings could not be read safely."];
        }
    }
    private void AddHistory(Guid requestId, CropPlanRequestStatus from, CropPlanRequestStatus to, string note)
    {
        dbContext.CropPlanRequestHistories.Add(new CropPlanRequestHistory { CropPlanRequestId = requestId, FromStatus = from, ToStatus = to, Note = note, ChangedByUserId = RequireUser(), CreatedByUserId = currentUser.UserId });
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new ApiException(HttpStatusCode.Unauthorized, "AUTH_REQUIRED", "Authentication is required.");

    private void RequireStaff()
    {
        if (currentUser.Role is ApplicationRole.Farmer or null)
        {
            throw new ApiException(HttpStatusCode.Forbidden, "STAFF_REQUIRED", "A staff role is required.");
        }
    }

    private void RequireAdmin()
    {
        if (currentUser.Role != ApplicationRole.Admin)
            throw new ApiException(HttpStatusCode.Forbidden, "ADMIN_REQUIRED", "An administrator is required.");
    }

    private void RequirePrePlantingViewer()
    {
        if (currentUser.Role is not (ApplicationRole.FieldOfficer or ApplicationRole.AgriculturalOfficer or ApplicationRole.Admin))
        {
            throw new ApiException(HttpStatusCode.Forbidden, "FIELD_ASSESSMENT_VIEWER_REQUIRED", "A Field Officer, Agricultural Officer or administrator is required.");
        }
    }

    private void RequireFieldOfficer()
    {
        if (currentUser.Role != ApplicationRole.FieldOfficer)
        {
            throw new ApiException(HttpStatusCode.Forbidden, "FIELD_OFFICER_REQUIRED", "A Field Officer is required to change a pre-planting assessment.");
        }
    }

    private static ApiException NotFound(string name) => new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");

    private static void Validate(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors));
    }

    private sealed class UnavailableAgenticAIClient : IAgenticAIClient
    {
        public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("AI service is not configured for this service instance.");

        public Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("AI service is not configured for this service instance.");
    }

    private static FarmResponse MapFarm(Farm farm) => new(farm.Id, farm.Name, farm.Location, farm.TotalArea, farm.OwnerUserId, farm.CreatedAt);
    private static FieldResponse MapField(Field field) => new(field.Id, field.FarmId, field.Name, field.Area, field.SoilType, field.IsActive);
    private static CropTypeResponse MapCropType(CropType cropType) => new(cropType.Id, cropType.Name, cropType.Description, cropType.IsActive);
    private static CropVarietyResponse MapVariety(CropVariety variety) => new(variety.Id, variety.CropTypeId, variety.Name, variety.IsActive);
    private static CropReferenceProfileResponse MapReferenceProfile(CropReferenceProfile profile) =>
        new(profile.Id, profile.CropTypeId, profile.VarietyName, profile.Region, profile.SourceName, profile.SourceUrl,
            profile.SourceVersion, profile.VerifiedAt, profile.IsActive, profile.Stages.Count, profile.Rules.Count);
    private static CropCycleResponse MapCycle(CropCycle cycle) => new(cycle.Id, cycle.FieldId, cycle.CropTypeId, cycle.PlannedStartDate, cycle.PlannedEndDate, cycle.Status);
    private static CropPlanRequestResponse MapRequest(CropPlanRequest request) =>
        new(request.Id, request.FarmId, request.FieldId, request.CropTypeId, request.RequestedByUserId,
            request.PreferredStartDate, request.PreferredEndDate, request.Budget, request.Objective,
            request.Status, request.CreatedAt, request.CropVarietyId, request.CultivationSeason,
            request.PreviousCropTypeId, JsonSerializer.Deserialize<List<string>>(request.PreviousKnownProblemsJson, JsonOptions) ?? []);
}





