using AgriAssist.Api.Data;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

/// <summary>Verifies the PostgreSQL-only filtered uniqueness rule for linked PrePlanting assessments.</summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class PrePlantingAssessmentPostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING";

    [PostgreSqlFact]
    public async Task Partial_index_allows_multiple_routine_inspections_but_only_one_pre_planting_assessment()
    {
        var connectionString = RequiredConnectionString();
        await using var dbContext = NewDbContext(connectionString);
        await dbContext.Database.MigrateAsync();
        var data = await SeedPlanAsync(dbContext);

        dbContext.FieldInspections.AddRange(
            NewInspection(data, InspectionPurpose.Routine),
            NewInspection(data, InspectionPurpose.Routine));
        await dbContext.SaveChangesAsync();

        dbContext.FieldInspections.Add(NewInspection(data, InspectionPurpose.PrePlanting));
        await dbContext.SaveChangesAsync();

        dbContext.FieldInspections.Add(NewInspection(data, InspectionPurpose.PrePlanting));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private static FieldInspection NewInspection(SeededPlan data, InspectionPurpose purpose) =>
        new()
        {
            FieldId = data.FieldId,
            CropPlanRequestId = data.RequestId,
            Purpose = purpose,
            InspectorUserId = data.FieldOfficerId,
            ScheduledAt = DateTime.UtcNow,
            Status = InspectionStatus.InProgress,
            Summary = purpose.ToString()
        };

    private static async Task<SeededPlan> SeedPlanAsync(AppDbContext dbContext)
    {
        var farmer = new AppUser
        {
            FullName = "PostgreSQL Assessment Farmer",
            Email = $"postgres-assessment-farmer-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.Farmer,
            IsActive = true
        };
        var fieldOfficer = new AppUser
        {
            FullName = "PostgreSQL Field Officer",
            Email = $"postgres-assessment-officer-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.FieldOfficer,
            IsActive = true
        };
        var farm = new Farm { Name = "Assessment Farm", Location = "North", TotalArea = 5, OwnerUser = farmer };
        var field = new Field { Farm = farm, Name = "Assessment Field", Area = 2, SoilType = "Loam", IsActive = true };
        var cropType = new CropType { Name = $"Rice-{Guid.NewGuid():N}", IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = cropType,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 12000,
            Objective = "Verify assessment uniqueness.",
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        dbContext.AddRange(farmer, fieldOfficer, farm, field, cropType, request);
        await dbContext.SaveChangesAsync();
        return new SeededPlan(request.Id, field.Id, fieldOfficer.Id);
    }

    private static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable)
        ?? throw new InvalidOperationException($"{ConnectionVariable} is required.");

    private static AppDbContext NewDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private sealed record SeededPlan(Guid RequestId, Guid FieldId, Guid FieldOfficerId);

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Set {ConnectionVariable} to run the PostgreSQL partial-index test.";
            }
        }
    }
}
