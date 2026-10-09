using AgriAssist.Api.Data;
using AgriAssist.Api.Models.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class FinalCultivationGuidePostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING";
    private const string AgentName = "FinalCultivationGuideAgent";

    [PostgreSqlFact]
    public async Task Concurrent_guide_steps_allow_one_active_record_per_workflow_revision()
    {
        var connectionString = RequiredConnectionString();
        Guid workflowId;
        await using (var setup = NewDbContext(connectionString))
        {
            await setup.Database.MigrateAsync();
            var user = new AppUser
            {
                FullName = "PostgreSQL Guide Farmer",
                Email = $"postgres-guide-{Guid.NewGuid():N}@example.test",
                PasswordHash = "hash",
                Role = ApplicationRole.Farmer,
                IsActive = true,
            };
            var workflow = new AgentWorkflow
            {
                InitiatedByUser = user,
                Objective = "Verify final guide uniqueness.",
                Status = AgentWorkflowStatus.Completed,
                CurrentStep = "Completed",
                CandidateRevision = 2,
                CompletedAt = DateTime.UtcNow,
            };
            setup.AddRange(user, workflow);
            await setup.SaveChangesAsync();
            workflowId = workflow.Id;
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 2).Select(async index =>
        {
            await using var dbContext = NewDbContext(connectionString);
            dbContext.AgentSteps.Add(new AgentStep
            {
                AgentWorkflowId = workflowId,
                AgentName = AgentName,
                StepName = "FarmerGuide",
                Sequence = 5 + index,
                CandidateRevision = 2,
                Status = AgentStepStatus.Running,
                StartedAt = DateTime.UtcNow,
            });
            await gate.Task;
            try
            {
                await dbContext.SaveChangesAsync();
                return "INSERTED";
            }
            catch (DbUpdateException)
            {
                return "CONFLICT";
            }
        }).ToArray();

        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result == "INSERTED");
        Assert.Single(results, result => result == "CONFLICT");
        await using var verification = NewDbContext(connectionString);
        Assert.Equal(1, await verification.AgentSteps.CountAsync(step =>
            step.AgentWorkflowId == workflowId
            && step.AgentName == AgentName
            && step.CandidateRevision == 2
            && !step.IsDeleted));
    }

    private static string RequiredConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable)
        ?? throw new InvalidOperationException($"{ConnectionVariable} is required.");

    private static AppDbContext NewDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to run the PostgreSQL final-guide uniqueness test.";
        }
    }
}
