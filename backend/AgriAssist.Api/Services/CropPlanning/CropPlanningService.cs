using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.CropPlanning;

public sealed class CropPlanningService(
    AppDbContext dbContext,
    ICurrentUserService currentUser,
    IRequestValidator<FarmRequest> farmValidator,
    IRequestValidator<FieldRequest> fieldValidator,
    IRequestValidator<CropTypeRequest> cropTypeValidator,
    IRequestValidator<CropCycleRequest> cropCycleValidator,
    IRequestValidator<CropPlanRequestCreate> createRequestValidator,
    IRequestValidator<CropPlanRequestUpdate> updateRequestValidator) : ICropPlanningService
{
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
        var farm = await ApplyFarmAccess(dbContext.Farms).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Farm");
        farm.Name = request.Name.Trim();
        farm.Location = request.Location.Trim();
        farm.TotalArea = request.TotalArea;
        farm.UpdatedAt = DateTime.UtcNow;
        farm.UpdatedByUserId = currentUser.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
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
        var farm = await ApplyFarmAccess(dbContext.Farms).SingleOrDefaultAsync(item => item.Id == request.FarmId, cancellationToken) ?? throw NotFound("Farm");
        var usedArea = await dbContext.Fields.Where(field => field.FarmId == request.FarmId && !field.IsDeleted).SumAsync(field => field.Area, cancellationToken);
        if (usedArea + request.Area > farm.TotalArea) throw new ApiException(HttpStatusCode.BadRequest, "FIELD_AREA_EXCEEDS_FARM", "Total field area cannot exceed farm area.");

        var field = new Field { FarmId = request.FarmId, Name = request.Name.Trim(), Area = request.Area, SoilType = request.SoilType.Trim(), IsActive = request.IsActive, CreatedByUserId = currentUser.UserId };
        dbContext.Fields.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapField(field);
    }

    public async Task<FieldResponse> UpdateFieldAsync(Guid id, FieldRequest request, CancellationToken cancellationToken)
    {
        Validate(fieldValidator.Validate(request));
        var field = await ApplyFieldAccess(dbContext.Fields).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Field");
        field.Name = request.Name.Trim();
        field.Area = request.Area;
        field.SoilType = request.SoilType.Trim();
        field.IsActive = request.IsActive;
        field.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapField(field);
    }

    public async Task<PagedResult<CropTypeResponse>> SearchCropTypesAsync(PagedQuery query, CancellationToken cancellationToken)
    {
        query.Normalize();
        var cropTypes = dbContext.CropTypes.AsNoTracking().Where(item => !item.IsDeleted);
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
        RequireStaff();
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
        RequireStaff();
        var cropType = await dbContext.CropTypes.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken) ?? throw NotFound("Crop type");
        cropType.Name = request.Name.Trim();
        cropType.Description = request.Description?.Trim();
        cropType.IsActive = request.IsActive;
        cropType.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapCropType(cropType);
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
        return await CreateRequestCoreAsync(request, CropPlanRequestStatus.PreliminaryGenerated, "Preliminary crop planning request generated without AI execution.", cancellationToken);
    }

    public async Task<CropPlanRequestResponse> UpdateCropPlanRequestAsync(Guid id, CropPlanRequestUpdate request, CancellationToken cancellationToken)
    {
        Validate(updateRequestValidator.Validate(request));
        var entity = await ApplyPlanRequestAccess(dbContext.CropPlanRequests).SingleOrDefaultAsync(item => item.Id == id, cancellationToken) ?? throw NotFound("Crop plan request");
        var previous = entity.Status;
        entity.PreferredStartDate = request.PreferredStartDate;
        entity.PreferredEndDate = request.PreferredEndDate;
        entity.Budget = request.Budget;
        entity.Objective = request.Objective.Trim();
        entity.Status = request.Status;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = currentUser.UserId;
        if (previous != request.Status) AddHistory(entity.Id, previous, request.Status, "Status updated.");
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

    private async Task<CropPlanRequestResponse> CreateRequestCoreAsync(CropPlanRequestCreate request, CropPlanRequestStatus status, string note, CancellationToken cancellationToken)
    {
        Validate(createRequestValidator.Validate(request));
        var userId = RequireUser();
        await EnsureFarmAccessAsync(request.FarmId, cancellationToken);
        if (request.FieldId.HasValue) await EnsureFieldAccessAsync(request.FieldId.Value, cancellationToken);
        await EnsureCropTypeAsync(request.CropTypeId, cancellationToken);

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

    private async Task EnsureFieldAccessAsync(Guid fieldId, CancellationToken cancellationToken)
    {
        if (!await ApplyFieldAccess(dbContext.Fields.AsNoTracking()).AnyAsync(item => item.Id == fieldId, cancellationToken)) throw NotFound("Field");
    }

    private async Task EnsureCropTypeAsync(Guid cropTypeId, CancellationToken cancellationToken)
    {
        if (!await dbContext.CropTypes.AsNoTracking().AnyAsync(item => item.Id == cropTypeId && item.IsActive && !item.IsDeleted, cancellationToken)) throw NotFound("Crop type");
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

    private static ApiException NotFound(string name) => new(HttpStatusCode.NotFound, "NOT_FOUND", $"{name} was not found.");

    private static void Validate(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ApiException(HttpStatusCode.BadRequest, "VALIDATION_ERROR", string.Join(" ", errors));
    }

    private static FarmResponse MapFarm(Farm farm) => new(farm.Id, farm.Name, farm.Location, farm.TotalArea, farm.OwnerUserId, farm.CreatedAt);
    private static FieldResponse MapField(Field field) => new(field.Id, field.FarmId, field.Name, field.Area, field.SoilType, field.IsActive);
    private static CropTypeResponse MapCropType(CropType cropType) => new(cropType.Id, cropType.Name, cropType.Description, cropType.IsActive);
    private static CropCycleResponse MapCycle(CropCycle cycle) => new(cycle.Id, cycle.FieldId, cycle.CropTypeId, cycle.PlannedStartDate, cycle.PlannedEndDate, cycle.Status);
    private static CropPlanRequestResponse MapRequest(CropPlanRequest request) => new(request.Id, request.FarmId, request.FieldId, request.CropTypeId, request.RequestedByUserId, request.PreferredStartDate, request.PreferredEndDate, request.Budget, request.Objective, request.Status, request.CreatedAt);
}
