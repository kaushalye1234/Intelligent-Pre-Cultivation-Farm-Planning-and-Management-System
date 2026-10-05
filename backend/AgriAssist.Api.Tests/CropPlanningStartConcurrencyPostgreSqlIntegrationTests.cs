using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class CropPlanningStartConcurrencyPostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING";

    [PostgreSqlFact]
    public async Task Concurrent_admin_starts_create_only_one_member_1_attempt()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new BlockingAiClient(providerEntered, releaseProvider);

        async Task<string> StartAsync()
        {
            await gate.Task;
            await using var db = NewDbContext(connectionString);
            try
            {
                await NewService(db, data.AdminId, client)
                    .StartAiWorkflowAsync(data.RequestId, CancellationToken.None);
                return "started";
            }
            catch (ApiException exception)
            {
                return exception.Code;
            }
        }

        var attempts = new[] { Task.Run(StartAsync), Task.Run(StartAsync) };
        gate.SetResult();
        await providerEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        releaseProvider.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results.Where(result => result == "started"));
        Assert.Single(results.Where(result =>
            result is "AI_WORKFLOW_ALREADY_ACTIVE" or "AI_WORKFLOW_ALREADY_COMPLETED"));
        await using var verification = NewDbContext(connectionString);
        Assert.Single(await verification.AgentWorkflows
            .Where(workflow => workflow.CropPlanRequestId == data.RequestId)
            .ToListAsync());
    }

    [PostgreSqlFact]
    public async Task Admin_cancellation_wins_when_coordinator_result_arrives_late()
    {
        var connectionString = RequiredConnectionString();
        var data = await SeedAsync(connectionString);
        var providerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new BlockingAiClient(providerEntered, releaseProvider);

        await using var startDb = NewDbContext(connectionString);
        var startTask = NewService(startDb, data.AdminId, client)
            .StartAiWorkflowAsync(data.RequestId, CancellationToken.None);
        await providerEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using (var cancelDb = NewDbContext(connectionString))
        {
            var cancelled = await NewService(cancelDb, data.AdminId, client)
                .CancelCropPlanRequestAsync(
                    data.RequestId,
                    new CropPlanCancellationRequest("Farmer selected a different crop."),
                    CancellationToken.None);
            Assert.Equal(CropPlanRequestStatus.Cancelled, cancelled.Status);
        }

        releaseProvider.SetResult();
        var conflict = await Assert.ThrowsAsync<ApiException>(() => startTask);
        Assert.Equal("CROP_PLAN_CANCELLED_DURING_AI", conflict.Code);

        await using var verification = NewDbContext(connectionString);
        var request = await verification.CropPlanRequests.SingleAsync(item => item.Id == data.RequestId);
        var workflow = await verification.AgentWorkflows.SingleAsync(item => item.CropPlanRequestId == data.RequestId);
        Assert.Equal(CropPlanRequestStatus.Cancelled, request.Status);
        Assert.Equal(AgentWorkflowStatus.Cancelled, workflow.Status);
        Assert.Equal("Cancelled", workflow.CurrentStep);
    }

    private static CropPlanningService NewService(AppDbContext db, Guid adminId, IAgenticAIClient client) =>
        new(
            db,
            new FixedCurrentUserService(adminId),
            new FarmRequestValidator(),
            new FieldRequestValidator(),
            new CropTypeRequestValidator(),
            new CropCycleRequestValidator(),
            new CropPlanRequestCreateValidator(),
            new CropPlanRequestUpdateValidator(),
            client);

    private static async Task<(Guid AdminId, Guid RequestId)> SeedAsync(string connectionString)
    {
        await using var db = NewDbContext(connectionString);
        await db.Database.MigrateAsync();
        var farmer = new AppUser
        {
            FullName = "Concurrency Farmer",
            Email = $"crop-plan-farmer-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.Farmer,
            IsActive = true
        };
        var admin = new AppUser
        {
            FullName = "Concurrency Admin",
            Email = $"crop-plan-admin-{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = ApplicationRole.Admin,
            IsActive = true
        };
        var farm = new Farm { Name = "Concurrency Farm", Location = "North", TotalArea = 5, OwnerUser = farmer };
        var field = new Field { Name = "Field A", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        var crop = new CropType { Name = $"Rice {Guid.NewGuid():N}", IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 15),
            PreferredEndDate = new DateOnly(2027, 2, 15),
            Budget = 100000,
            Objective = "Test concurrent Member 1 start.",
            Status = CropPlanRequestStatus.Submitted
        };
        db.AddRange(admin, request);
        await db.SaveChangesAsync();
        return (admin.Id, request.Id);
    }

    private static AppDbContext NewDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable)
        ?? throw new InvalidOperationException($"{ConnectionVariable} is required.");

    private sealed class BlockingAiClient(
        TaskCompletionSource entered,
        TaskCompletionSource release) : IAgenticAIClient
    {
        public async Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(
            CropPlanningCoordinatorInput input,
            CancellationToken cancellationToken)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new CropPlanningCoordinatorOutput(
                input.WorkflowId,
                "Planned",
                false,
                [],
                "Available",
                "Safe objective summary.",
                [
                    new CropPlanningDelegatedStepResponse(1, "FieldAnalysis", "CropFieldAnalysisAgent"),
                    new CropPlanningDelegatedStepResponse(2, "WeatherResourceAnalysis", "WeatherResourceAgent"),
                    new CropPlanningDelegatedStepResponse(3, "Scheduling", "SchedulingValidationAgent")
                ]);
        }

        public Task<FieldAnalysisOutput> RunFieldAnalysisAsync(
            FieldAnalysisInput input,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public ApplicationRole? Role => ApplicationRole.Admin;
        public bool IsInRole(ApplicationRole role) => role == ApplicationRole.Admin;
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to run the PostgreSQL concurrency test.";
        }
    }
}
