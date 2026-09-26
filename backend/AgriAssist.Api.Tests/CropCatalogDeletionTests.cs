using System.Net;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class CropCatalogDeletionTests
{
    [Fact]
    public async Task Unused_crop_can_be_deleted_without_removing_its_audit_record()
    {
        await using var db = NewDbContext();
        var crop = new CropType { Name = "Unused crop", IsActive = true };
        db.CropTypes.Add(crop);
        await db.SaveChangesAsync();

        await NewService(db).DeleteCropTypeAsync(crop.Id, CancellationToken.None);

        var stored = await db.CropTypes.SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.False(stored.IsActive);
    }

    [Theory]
    [InlineData("variety")]
    [InlineData("cycle")]
    [InlineData("plan")]
    [InlineData("previous-crop")]
    [InlineData("reference")]
    public async Task Referenced_crop_cannot_be_deleted(string dependency)
    {
        await using var db = NewDbContext();
        var crop = new CropType { Name = $"Referenced {dependency}", IsActive = true };
        db.CropTypes.Add(crop);
        await AddCropDependencyAsync(db, crop, dependency);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            NewService(db).DeleteCropTypeAsync(crop.Id, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("CROP_TYPE_IN_USE", exception.Code);
        Assert.Contains("Deactivate", exception.Message);
        Assert.False(crop.IsDeleted);
    }

    [Fact]
    public async Task Unused_variety_can_be_deleted_without_removing_its_audit_record()
    {
        await using var db = NewDbContext();
        var crop = new CropType { Name = "Rice", IsActive = true };
        var variety = new CropVariety { CropType = crop, Name = "Unused variety", IsActive = true };
        db.AddRange(crop, variety);
        await db.SaveChangesAsync();

        await NewService(db).DeleteCropVarietyAsync(variety.Id, CancellationToken.None);

        var stored = await db.CropVarieties.SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.False(stored.IsActive);
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("reference")]
    public async Task Referenced_variety_cannot_be_deleted(string dependency)
    {
        await using var db = NewDbContext();
        var crop = new CropType { Name = "Rice", IsActive = true };
        var variety = new CropVariety { CropType = crop, Name = "Bg 352", IsActive = true };
        db.AddRange(crop, variety);
        await db.SaveChangesAsync();

        if (dependency == "plan")
        {
            var plan = await NewPlanAsync(db, crop, variety);
            plan.Status = CropPlanRequestStatus.Approved;
        }
        else
        {
            db.CropReferenceProfiles.Add(NewReference(crop, variety.Name));
        }
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            NewService(db).DeleteCropVarietyAsync(variety.Id, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("CROP_VARIETY_IN_USE", exception.Code);
        Assert.Contains("Deactivate", exception.Message);
        Assert.False(variety.IsDeleted);
    }

    [Fact]
    public async Task Deactivation_keeps_crop_and_variety_available_for_admin_history()
    {
        await using var db = NewDbContext();
        var crop = new CropType { Name = "Rice", IsActive = true };
        var variety = new CropVariety { CropType = crop, Name = "Bg 352", IsActive = true };
        db.AddRange(crop, variety);
        await db.SaveChangesAsync();
        var service = NewService(db);

        await service.UpdateCropTypeAsync(crop.Id, new CropTypeRequest(crop.Name, null, false), CancellationToken.None);
        await service.UpdateCropVarietyAsync(variety.Id, new CropVarietyRequest(crop.Id, variety.Name, false), CancellationToken.None);

        Assert.False(crop.IsActive);
        Assert.False(variety.IsActive);
        Assert.False(crop.IsDeleted);
        Assert.False(variety.IsDeleted);

        Assert.Empty((await service.SearchCropTypesAsync(new PagedQuery(), CancellationToken.None)).Items);
        Assert.Single((await service.SearchCropTypesAsync(new PagedQuery(), CancellationToken.None, includeInactive: true)).Items);
        Assert.Empty((await service.SearchCropVarietiesAsync(new PagedQuery(), crop.Id, CancellationToken.None)).Items);
        Assert.Single((await service.SearchCropVarietiesAsync(new PagedQuery(), crop.Id, CancellationToken.None, includeInactive: true)).Items);
    }

    private static async Task AddCropDependencyAsync(AppDbContext db, CropType crop, string dependency)
    {
        switch (dependency)
        {
            case "variety":
                db.CropVarieties.Add(new CropVariety { CropType = crop, Name = "Child variety" });
                break;
            case "cycle":
            {
                var (_, field) = await NewFarmContextAsync(db);
                db.CropCycles.Add(new CropCycle
                {
                    CropType = crop,
                    Field = field,
                    PlannedStartDate = new DateOnly(2026, 10, 1),
                    PlannedEndDate = new DateOnly(2027, 1, 1),
                });
                break;
            }
            case "plan":
            {
                var plan = await NewPlanAsync(db, crop);
                plan.Status = CropPlanRequestStatus.Approved;
                break;
            }
            case "previous-crop":
            {
                var currentCrop = new CropType { Name = "Current crop" };
                db.CropTypes.Add(currentCrop);
                var plan = await NewPlanAsync(db, currentCrop);
                plan.PreviousCropType = crop;
                break;
            }
            case "reference":
                db.CropReferenceProfiles.Add(NewReference(crop));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dependency));
        }
    }

    private static CropReferenceProfile NewReference(CropType crop, string? varietyName = null) => new()
    {
        CropType = crop,
        VarietyName = varietyName,
        SourceName = "Department of Agriculture",
        SourceVersion = "2026.1",
        VerifiedAt = DateTime.UtcNow,
    };

    private static async Task<CropPlanRequest> NewPlanAsync(AppDbContext db, CropType crop, CropVariety? variety = null)
    {
        var (farmer, field) = await NewFarmContextAsync(db);
        var plan = new CropPlanRequest
        {
            Farm = field.Farm,
            Field = field,
            CropType = crop,
            CropVariety = variety,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 10000,
            Objective = "Preserve crop planning history",
        };
        db.CropPlanRequests.Add(plan);
        return plan;
    }

    private static Task<(AppUser Farmer, Field Field)> NewFarmContextAsync(AppDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var farmer = new AppUser
        {
            FullName = "Catalog test farmer",
            Email = $"farmer-{suffix}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.Farmer,
            IsActive = true,
        };
        var farm = new Farm { Name = $"Farm {suffix}", Location = "North", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = $"Field {suffix}", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        db.AddRange(farmer, farm, field);
        return Task.FromResult((farmer, field));
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CropPlanningService NewService(AppDbContext db) =>
        new(
            db,
            new TestCurrentUserService(ApplicationRole.Admin),
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator());
}
