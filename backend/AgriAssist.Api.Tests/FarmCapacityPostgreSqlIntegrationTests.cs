using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

// Shares a collection with the inventory tests: both migrate the same database, so they must not run in parallel.
[Collection(PostgreSqlCollection.Name)]
public sealed class FarmCapacityPostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING";

    [PostgreSqlFact]
    public async Task Concurrent_active_field_creation_cannot_exceed_farm_capacity()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString, 10m);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(1, 8).Select(async index =>
        {
            await gate.Task;
            await using var dbContext = NewDbContext(connectionString);
            var service = NewService(dbContext, data.FarmerId);
            try
            {
                await service.CreateFieldAsync(
                    new FieldRequest(data.FarmId, $"Concurrent {index}", 2m, "Loam", true),
                    CancellationToken.None);
                return true;
            }
            catch (ApiException exception) when (exception.Code == "FIELD_AREA_EXCEEDS_FARM")
            {
                return false;
            }
        }).ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        await using var verification = NewDbContext(connectionString);
        var activeFields = verification.Fields.Where(field => field.FarmId == data.FarmId && field.IsActive && !field.IsDeleted);
        Assert.Equal(5, results.Count(result => result));
        Assert.Equal(3, results.Count(result => !result));
        Assert.Equal(10m, await activeFields.SumAsync(field => field.Area));
    }

    [PostgreSqlFact]
    public async Task Concurrent_field_creation_and_capacity_reduction_preserve_the_invariant()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString, 10m, existingActiveArea: 2m);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var createTask = Task.Run(async () =>
        {
            await gate.Task;
            await using var dbContext = NewDbContext(connectionString);
            try
            {
                await NewService(dbContext, data.FarmerId).CreateFieldAsync(
                    new FieldRequest(data.FarmId, "Competing field", 3m, "Clay", true),
                    CancellationToken.None);
                return true;
            }
            catch (ApiException exception) when (exception.Code == "FIELD_AREA_EXCEEDS_FARM")
            {
                return false;
            }
        });
        var reduceTask = Task.Run(async () =>
        {
            await gate.Task;
            await using var dbContext = NewDbContext(connectionString);
            try
            {
                await NewService(dbContext, data.FarmerId).UpdateFarmAsync(
                    data.FarmId,
                    new FarmRequest("Concurrent Capacity Farm", "North", 4m, null),
                    CancellationToken.None);
                return true;
            }
            catch (ApiException exception) when (exception.Code == "FIELD_AREA_EXCEEDS_FARM")
            {
                return false;
            }
        });

        gate.SetResult();
        var results = await Task.WhenAll(createTask, reduceTask);

        await using var verification = NewDbContext(connectionString);
        var farm = await verification.Farms.SingleAsync(item => item.Id == data.FarmId);
        var allocatedArea = await verification.Fields
            .Where(field => field.FarmId == data.FarmId && field.IsActive && !field.IsDeleted)
            .SumAsync(field => field.Area);
        Assert.Single(results.Where(result => result));
        Assert.True(allocatedArea <= farm.TotalArea);
    }

    private static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable)
        ?? throw new InvalidOperationException($"{ConnectionVariable} is required.");

    private static AppDbContext NewDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

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

    private static async Task<(Guid FarmerId, Guid FarmId)> SeedAsync(
        string connectionString,
        decimal totalArea,
        decimal? existingActiveArea = null)
    {
        await using var dbContext = NewDbContext(connectionString);
        await dbContext.Database.MigrateAsync();
        var farmer = new AppUser
        {
            FullName = "PostgreSQL Capacity Farmer",
            Email = $"postgres-capacity-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.Farmer,
            IsActive = true
        };
        var farm = new Farm { Name = "Concurrent Capacity Farm", Location = "North", TotalArea = totalArea, OwnerUser = farmer };
        dbContext.AddRange(farmer, farm);
        if (existingActiveArea.HasValue)
        {
            dbContext.Fields.Add(new Field
            {
                Farm = farm,
                Name = "Existing active field",
                SoilType = "Loam",
                Area = existingActiveArea.Value,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();
        return (farmer.Id, farm.Id);
    }

    private sealed class CapacityTestCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role => ApplicationRole.Farmer;
        public bool IsInRole(ApplicationRole role) => role == ApplicationRole.Farmer;
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Set {ConnectionVariable} to run the PostgreSQL concurrency test.";
            }
        }
    }
}
