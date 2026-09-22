using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

public sealed class FarmCapacityBusinessRuleTests
{
    [Fact]
    public async Task Field_creation_uses_total_active_allocated_area()
    {
        await using var dbContext = NewDbContext();
        var data = await SeedAsync(dbContext, totalArea: 5m);
        dbContext.Fields.AddRange(
            new Field { FarmId = data.FarmId, Name = "Active", SoilType = "Loam", Area = 4m, IsActive = true },
            new Field { FarmId = data.FarmId, Name = "Inactive", SoilType = "Loam", Area = 5m, IsActive = false });
        await dbContext.SaveChangesAsync();
        var service = NewService(dbContext, data.FarmerId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.CreateFieldAsync(
            new FieldRequest(data.FarmId, "Too large", 2m, "Clay", true),
            CancellationToken.None));

        Assert.Equal("FIELD_AREA_EXCEEDS_FARM", exception.Code);
        Assert.Equal(2, await dbContext.Fields.CountAsync());
    }

    [Fact]
    public async Task Activating_or_enlarging_a_field_cannot_overallocate_the_farm()
    {
        await using var dbContext = NewDbContext();
        var data = await SeedAsync(dbContext, totalArea: 5m);
        var active = new Field { FarmId = data.FarmId, Name = "Active", SoilType = "Loam", Area = 4m, IsActive = true };
        var inactive = new Field { FarmId = data.FarmId, Name = "Inactive", SoilType = "Loam", Area = 1m, IsActive = false };
        dbContext.Fields.AddRange(active, inactive);
        await dbContext.SaveChangesAsync();
        var service = NewService(dbContext, data.FarmerId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.UpdateFieldAsync(
            inactive.Id,
            new FieldRequest(data.FarmId, inactive.Name, 2m, inactive.SoilType, true),
            CancellationToken.None));

        Assert.Equal("FIELD_AREA_EXCEEDS_FARM", exception.Code);
        Assert.False(inactive.IsActive);
    }

    [Fact]
    public async Task Farm_capacity_cannot_be_reduced_below_active_allocated_area()
    {
        await using var dbContext = NewDbContext();
        var data = await SeedAsync(dbContext, totalArea: 10m);
        dbContext.Fields.Add(new Field { FarmId = data.FarmId, Name = "Active", SoilType = "Loam", Area = 6m, IsActive = true });
        await dbContext.SaveChangesAsync();
        var service = NewService(dbContext, data.FarmerId);

        var exception = await Assert.ThrowsAsync<ApiException>(() => service.UpdateFarmAsync(
            data.FarmId,
            new FarmRequest("Capacity Farm", "North", 5m, null),
            CancellationToken.None));

        Assert.Equal("FIELD_AREA_EXCEEDS_FARM", exception.Code);
        Assert.Equal(10m, (await dbContext.Farms.SingleAsync()).TotalArea);
    }

    private static AppDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CropPlanningService NewService(AppDbContext dbContext, Guid farmerId) =>
        new(
            dbContext,
            new CapacityTestCurrentUser(farmerId),
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator());

    private static async Task<(Guid FarmerId, Guid FarmId)> SeedAsync(AppDbContext dbContext, decimal totalArea)
    {
        var farmer = new AppUser
        {
            FullName = "Capacity Farmer",
            Email = $"capacity-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.Farmer,
            IsActive = true
        };
        var farm = new Farm { Name = "Capacity Farm", Location = "North", TotalArea = totalArea, OwnerUser = farmer };
        dbContext.AddRange(farmer, farm);
        await dbContext.SaveChangesAsync();
        return (farmer.Id, farm.Id);
    }

    private sealed class CapacityTestCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role => ApplicationRole.Farmer;
        public bool IsInRole(ApplicationRole role) => role == ApplicationRole.Farmer;
    }
}
