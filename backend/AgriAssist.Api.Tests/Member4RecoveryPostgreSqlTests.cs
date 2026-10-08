using System.Globalization;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Dtos.TaskApproval;
using AgriAssist.Api.Dtos.FinalCultivationGuide;
using AgriAssist.Api.ExternalServices.AgenticAI;
using AgriAssist.Api.ExternalServices.Weather;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Models.TaskApproval;
using AgriAssist.Api.Services.CropPlanning;
using AgriAssist.Api.Services.Resources;
using AgriAssist.Api.Services.TaskApproval;
using AgriAssist.Api.Services.FinalCultivationGuide;
using AgriAssist.Api.Services.Shared;
using AgriAssist.Api.Validators.CropPlanning;
using AgriAssist.Api.Validators.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.Extensions.Logging.Abstractions;
using static AgriAssist.Api.Tests.WeatherResourceTestData;

namespace AgriAssist.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class Member4RecoveryPostgreSqlTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string DemoPassword = "RecoveryDemo@2026!"; // Disposable synthetic accounts only.

    [PostgreSqlFact]
    public async Task New_recovery_run_executes_all_member_services_then_approval_and_farmer_guide()
    {
        var connection = Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")!;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        var seed = await SeedAsync(db, reserved: 0, cropTypeName: "SYNTHETIC RECOVERY " + Guid.NewGuid().ToString("N"));
        var farmer = await db.Users.SingleAsync(item => item.Id == seed.FarmerId);
        farmer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        farmer.PasswordChangedAt = DateTime.UtcNow;
        var run = Guid.NewGuid().ToString("N");
        AppUser Account(ApplicationRole role) => new() { FullName = "Synthetic Recovery " + role,
            Email = "recovery-" + role.ToString().ToLowerInvariant() + "-" + run + "@example.test",
            Role = role, PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword), PasswordChangedAt = DateTime.UtcNow };
        var admin = Account(ApplicationRole.Admin);
        var fieldOfficer = Account(ApplicationRole.FieldOfficer);
        var resourceOfficer = Account(ApplicationRole.ResourceOfficer);
        var officer = Account(ApplicationRole.AgriculturalOfficer);
        db.AddRange(admin, fieldOfficer, resourceOfficer, officer);
        var plan = await db.CropPlanRequests.SingleAsync(item => item.Id == seed.RequestId);
        plan.PreferredStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        plan.PreferredEndDate = plan.PreferredStartDate.AddDays(120);
        plan.Objective = "SYNTHETIC MEMBER 4 RECOVERY DEMO " + run;
        var old = await db.AgentWorkflows.SingleAsync(item => item.Id == seed.WorkflowId);
        old.Status = AgentWorkflowStatus.MissingDependency;
        old.CurrentStep = "SchedulingValidationAgent";
        var reference = await CropReferenceVerificationTests.PrepareDraft(db, seed.CropTypeId, seed.ResourceId);
        reference.Stages[0].StageName = "Planting";
        reference.Stages[0].TypicalMinDays = 3;
        reference.Stages[0].TypicalMaxDays = 5;
        var irrigation = new CropRuleReference { CropReferenceProfileId = reference.Id, RuleType = "IrrigationSchedule",
            RuleKey = "Synthetic irrigation", SourceName = reference.SourceName, SourceUrl = reference.SourceUrl,
            StructuredValueJson = """{"dayOffsetFromPlanting":1,"startTimeUtc":"06:00","durationMinutes":30}""" };
        db.Add(irrigation);
        await db.SaveChangesAsync();

        var verified = await CropReferenceVerificationTests.CreateService(db, officer.Id).VerifyAsync(reference.Id,
            new VerifyReferenceRequest(seed.FieldId, WaterRegime.Irrigated, "Synthetic officer field and source review.", 1, true), CancellationToken.None);
        Assert.Equal(CropReferenceVerificationState.Verified, verified.VerificationState);
        var adminPlanning = Planning(db, new CurrentUser(admin));
        var replacement = await adminPlanning.StartReplacementWorkflowAsync(plan.Id,
            new StartReplacementRequest(old.Id, reference.Id, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal("Planned", replacement.Status);
        var fieldPlanning = Planning(db, new CurrentUser(fieldOfficer));
        await fieldPlanning.SavePrePlantingAssessmentAsync(plan.Id, ValidAssessment(), CancellationToken.None);
        await fieldPlanning.SubmitPrePlantingAssessmentAsync(plan.Id, CancellationToken.None);
        var fieldAnalysis = await fieldPlanning.RunFieldAnalysisAsync(plan.Id, CancellationToken.None);
        Assert.Equal("Analyzed", fieldAnalysis.Status);
        var resourceUser = new CurrentUser(resourceOfficer);
        var resourceWorkflow = new WeatherResourceWorkflowService(db, resourceUser, Planning(db, resourceUser),
            new ToolCallingAgent(db), NullLogger<WeatherResourceWorkflowService>.Instance);
        var resourceAnalysis = await resourceWorkflow.RunAsync(plan.Id, CancellationToken.None);
        Assert.Equal("Analyzed", resourceAnalysis.Status);
        Assert.Equal("Sufficient", resourceAnalysis.RequirementStatus);
        var officerUser = new CurrentUser(officer);
        var resources = new ResourceService(db, officerUser, new ResourceCategoryRequestValidator(), new SupplierRequestValidator(),
            new ResourceRequestValidator(), new InventoryStockRequestValidator(), new ResourceReservationRequestValidator());
        var approval = new WorkflowApprovalService(db, officerUser, new SchedulingClient(), resources);
        var candidate = await approval.GenerateCandidateAsync(replacement.WorkflowId, CancellationToken.None);
        Assert.Equal(AgentWorkflowStatus.PendingOfficerApproval, candidate.Workflow.Status);
        Assert.False(await db.FarmTasks.AnyAsync(item => item.GeneratedByWorkflowId == replacement.WorkflowId));
        var resourceOutput = await resourceWorkflow.GetResultAsync(plan.Id, CancellationToken.None);
        Assert.Equal(reference.Id, resourceOutput.RequirementSource!.CropReferenceProfileId);
        var schedulingStep = candidate.Steps.Single(item => item.AgentName == "SchedulingValidationAgent");
        var schedulingInput = schedulingStep.Input.Deserialize<SchedulingValidationInput>(Json)!;
        Assert.Equal(reference.Id, schedulingInput.Evidence!.ProfileId);

        var decision = await approval.ApproveAsync(replacement.WorkflowId,
            new WorkflowDecisionRequest(candidate.Workflow.CandidateRevision, candidate.Workflow.Version, Guid.NewGuid().ToString(),
                "Synthetic Agricultural Officer approved this disposable demonstration."), CancellationToken.None);
        Assert.Equal(ApprovalDecisionType.Approved, decision.Decision);
        Assert.Equal(2, decision.FarmTaskIds.Count);
        Assert.Single(decision.IrrigationScheduleIds);
        Assert.Single(decision.ReservationIds);
        var guide = await new FinalCultivationGuideService(db, new GuideClient(), NullLogger<FinalCultivationGuideService>.Instance)
            .GenerateAsync(replacement.WorkflowId, decision.CandidateRevision, CancellationToken.None);
        Assert.Equal("Ready", guide.Status);
        var farmerPlan = await Planning(db, new CurrentUser(farmer)).GetApprovedPlanAsync(plan.Id, CancellationToken.None);
        Assert.Equal("Ready", farmerPlan.FinalGuideStatus);
        Assert.NotNull(farmerPlan.FinalGuideGeneratedAt);
        var denied = await Assert.ThrowsAsync<ApiException>(() => Planning(db,
            new CurrentUser(new AppUser { Id = Guid.NewGuid(), Role = ApplicationRole.Farmer })).GetApprovedPlanAsync(plan.Id, CancellationToken.None));
        Assert.Equal("NOT_FOUND", denied.Code);
        Assert.Equal(AgentWorkflowStatus.MissingDependency, (await db.AgentWorkflows.AsNoTracking().SingleAsync(item => item.Id == old.Id)).Status);
        var resolution = await approval.GetEvidenceResolutionAsync(old.Id, CancellationToken.None);
        Assert.Equal(replacement.WorkflowId, resolution.SuccessorWorkflowId);
        Assert.Equal("Farmer", resolution.NextResponsibleRole);
        var immutable = await Assert.ThrowsAsync<ApiException>(() => approval.GenerateCandidateAsync(old.Id, CancellationToken.None));
        Assert.Equal("WORKFLOW_SUPERSEDED", immutable.Code);
        var rollback = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>()
            .MigrateAsync("20261007113120_AddCropReferenceVerification"));
        Assert.Contains("Cannot roll back pinned workflows", rollback.MessageText);
        Console.WriteLine("SYNTHETIC_RECOVERY_DEMO " + JsonSerializer.Serialize(new { planId = plan.Id,
            blockedWorkflowId = old.Id, replacementWorkflowId = replacement.WorkflowId, profileId = reference.Id,
            officerEmail = officer.Email, adminEmail = admin.Email, farmerEmail = farmer.Email }, Json));
    }

    private static CropPlanningService Planning(AppDbContext db, ICurrentUserService user) => new(db, user,
        new FarmRequestValidator(), new FieldRequestValidator(), new CropTypeRequestValidator(), new CropCycleRequestValidator(),
        new CropPlanRequestCreateValidator(), new CropPlanRequestUpdateValidator(), new MemberClients());

    private sealed class CurrentUser(AppUser user) : ICurrentUserService
    {
        public Guid? UserId => user.Id;
        public ApplicationRole? Role => user.Role;
        public bool IsInRole(ApplicationRole role) => role == user.Role;
    }
    private sealed class StubWeatherService : IWeatherService
    {
        public Task<WeatherForecastResponse> GetForecastAsync(string location, CancellationToken cancellationToken) =>
            Task.FromResult(new WeatherForecastResponse(location, true, "Synthetic fixture",
                [new WeatherDayResponse(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), 23, 31, 12, 5, "Synthetic moderate rain")]));
    }
    private sealed class MemberClients : IAgenticAIClient
    {
        public Task<CropPlanningCoordinatorOutput> RunCropPlanningCoordinatorAsync(CropPlanningCoordinatorInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new CropPlanningCoordinatorOutput(input.WorkflowId, "Planned", false, [], "Available", "Synthetic coordination.",
                [new CropPlanningDelegatedStepResponse(1, "FieldAnalysis", "CropFieldAnalysisAgent"),
                 new CropPlanningDelegatedStepResponse(2, "WeatherResourceAnalysis", "WeatherResourceAgent"),
                 new CropPlanningDelegatedStepResponse(3, "Scheduling", "SchedulingValidationAgent")]));
        public Task<FieldAnalysisOutput> RunFieldAnalysisAsync(FieldAnalysisInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new FieldAnalysisOutput(input.WorkflowId, "Analyzed", false, [],
                new FieldAnalysisFieldConditionResponse("Synthetic submitted field evidence.", [input.PrePlantingInspectionId]),
                [], "Medium", "SuitableWithConditions", "Soil type Loamy; condition Good; moisture Moist.",
                "Water availability Adequate; main source Canal; irrigation Available; reliability Reliable.",
                "Drainage condition Good; waterlogging risk Low.", ["Complete the recorded land preparation before planting."],
                "ReadyWithMinorPreparation", [PrePlantingRisk.LandPreparationRequired], ["Recheck field readiness before planting activities begin."]));
    }
    private sealed class GuideClient : IFinalCultivationGuideAIClient
    {
        public Task<FinalCultivationGuideOutputDto> GenerateFinalCultivationGuideAsync(FinalCultivationGuideInputDto input, CancellationToken cancellationToken) =>
            Task.FromResult(new FinalCultivationGuideOutputDto(1, input.WorkflowId, input.ApprovedRevision,
                ["Follow the approved activities and consult the officer if field conditions change."], null, [], [],
                ["Prepare storage and inspect crop readiness with the officer."], "This guide explains the approved synthetic plan."));
    }
    private sealed class SchedulingClient : ISchedulingValidationAIClient
    {
        public Task<SchedulingValidationOutput> RunSchedulingValidationAsync(SchedulingValidationInput input, CancellationToken cancellationToken)
        {
            var evidence = input.Evidence!;
            var preparations = input.FieldAnalysisOutput.GetProperty("fieldPreparationRequirements").EnumerateArray().Select(item => item.GetString()!).ToArray();
            var firstDay = input.PreferredStartDate;
            var plantingDay = firstDay.AddDays(preparations.Length > 0 ? 1 : 0);
            var tasks = preparations.Select(item => new SchedulingCandidateTask(input.FarmId, "Review recorded field preparation requirement",
                "Review the persisted synthetic preparation.", At(firstDay, "08:00"), input.AssignedToUserId,
                "Member 2 recorded: " + item, [new SchedulingSource("FieldAnalysis", evidence.FieldAnalysisStepId!.Value, "Synthetic field analysis")])).ToList();
            var stageDay = plantingDay;
            foreach (var stage in evidence.Stages)
            {
                tasks.Add(new SchedulingCandidateTask(input.FarmId, "Review " + stage.StageName + " stage", "Review the synthetic verified stage.",
                    At(stageDay, "08:00"), input.AssignedToUserId, "Verified stage " + stage.Sequence + " follows the profile's minimum stage durations.",
                    [new SchedulingSource("CropStage", stage.Id, evidence.SourceName!, evidence.ProfileId, evidence.SourceVersion, evidence.VerifiedAt)]));
                stageDay = stageDay.AddDays(stage.TypicalMinDays!.Value);
            }
            var irrigation = evidence.IrrigationRules.Select(rule => new SchedulingCandidateIrrigation(input.FieldId!.Value,
                At(plantingDay.AddDays(rule.DayOffsetFromPlanting), rule.StartTimeUtc), rule.DurationMinutes, "Synthetic source-attributed irrigation.",
                "Verified irrigation rule " + rule.RuleKey + " specifies this offset, UTC time and duration.",
                [new SchedulingSource("IrrigationRule", rule.Id, rule.SourceName, evidence.ProfileId, evidence.SourceVersion, rule.VerifiedAt)])).ToArray();
            var checks = input.WeatherResourceOutput.GetProperty("resourceChecks").EnumerateArray().ToArray();
            var reservations = input.WeatherResourceOutput.GetProperty("resourceRequirements").EnumerateArray().Select(rule => {
                var quantity = rule.GetProperty("requiredQuantity").GetDecimal();
                var unit = rule.GetProperty("unit").GetString()!;
                var resourceId = rule.GetProperty("resourceId").GetGuid();
                var stock = checks.Single(item => item.GetProperty("resourceId").GetGuid() == resourceId);
                return new SchedulingCandidateReservation(stock.GetProperty("inventoryStockId").GetGuid(), quantity, "Synthetic approved resource use", null,
                    "Member 3 verified " + quantity.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit + " required and available.",
                    [new SchedulingSource("ResourceRequirement", rule.GetProperty("ruleId").GetGuid(), rule.GetProperty("resourceName").GetString()!,
                        evidence.ProfileId, evidence.SourceVersion, evidence.VerifiedAt)]);
            }).ToArray();
            return Task.FromResult(new SchedulingValidationOutput(input.WorkflowId, input.CandidateRevision, "CandidateReady", true, true,
                ["Synthetic source evidence only. Officer approval required."], tasks, irrigation, reservations, null,
                [new SchedulingConstraint("HUMAN_APPROVAL", "Blocking", "Officer approval is required.")], 2));
        }
        private static DateTime At(DateOnly day, string time) => DateTime.SpecifyKind(day.ToDateTime(TimeOnly.Parse(time)), DateTimeKind.Utc);
    }
    private static PrePlantingAssessmentRequest ValidAssessment() =>
        new()
        {
            SoilType = PrePlantingSoilType.Loamy,
            SoilCondition = PrePlantingSoilCondition.Good,
            SoilMoisture = PrePlantingSoilMoisture.Moist,
            SoilNotes = "Moist loam with no visible compaction.",
            WaterAvailability = PrePlantingWaterAvailability.Adequate,
            MainWaterSource = "Canal",
            IrrigationAvailability = PrePlantingIrrigationAvailability.Available,
            WaterReliability = PrePlantingWaterReliability.Reliable,
            WaterConcerns = "Supply should be rechecked before sowing.",
            DrainageCondition = PrePlantingDrainageCondition.Good,
            WaterloggingRisk = PrePlantingWaterloggingRisk.Low,
            DrainageNotes = "Drainage channels are clear.",
            GeneralFieldCondition = PrePlantingGeneralFieldCondition.ClearAndPrepared,
            GeneralFieldNotes = "Field is cleared and level.",
            PlantingReadiness = PrePlantingPlantingReadiness.ReadyWithMinorPreparation,
            IdentifiedRisks = [PrePlantingRisk.LandPreparationRequired],
            RiskNotes = "Final harrowing remains.",
            OfficerNotes = "Recheck the low area before sowing."
        };

    private sealed class ToolCallingAgent(AppDbContext db, Func<WeatherResourceOutput, WeatherResourceOutput>? tamper = null, bool callTools = true) : IWeatherResourceAIClient
    {
        public WeatherResourceInput? Input { get; private set; }

        public async Task<WeatherResourceOutput> RunWeatherResourceAnalysisAsync(WeatherResourceInput input, CancellationToken cancellationToken)
        {
            Input = input;
            var workflow = await db.AgentWorkflows.AsNoTracking().SingleAsync(item => item.Id == input.WorkflowId, cancellationToken);
            var requirements = await new CropResourceRequirementService(db, TestConfiguration()).GetRequirementsAsync(input.CropPlanRequestId, workflow.RequiredCropReferenceProfileId, cancellationToken);
            var tools = new WeatherResourceToolService(db, new StubWeatherService());
            var ids = requirements.Requirements
                .Where(rule => rule.Status == RequirementCalculationStatus.Calculated && rule.ResourceMatch == ResourceMatchStatus.Matched)
                .Select(rule => rule.ResourceId!.Value).ToArray();
            var stocks = await tools.GetResourceAvailabilityAsync(ids, cancellationToken);
            var weather = await tools.GetWeatherForecastAsync(input.WorkflowId, cancellationToken);
            if (callTools)
            {
                await RecordAsync(input, WeatherResourceToolNames.GetCropResourceRequirements, new { cropPlanRequestId = input.CropPlanRequestId, workflowId = input.WorkflowId, agentStepId = input.AgentStepId }, requirements);
                await RecordAsync(input, WeatherResourceToolNames.GetResourceAvailability, new { resourceIds = ids, workflowId = input.WorkflowId, agentStepId = input.AgentStepId }, stocks);
                await RecordAsync(input, WeatherResourceToolNames.GetWeatherForecast, new { workflowId = input.WorkflowId, agentStepId = input.AgentStepId }, weather);
            }

            var byResource = stocks.ToDictionary(stock => stock.ResourceId);
            var assessments = requirements.Requirements.Select(rule => WeatherResourceWorkflowService.ExpectedAssessment(rule, byResource, ids)!).ToList();
            var overall = WeatherResourceWorkflowService.OverallStatus(requirements, assessments);
            if (requirements.Requirements.Count == 0)
                assessments.Add(new ResourceRequirementAssessment(null, null, requirements.CropName, null, null, null, null, null, null, ResourceRequirementStatus.Unknown, null, requirements.Reason));
            var comparable = requirements.Requirements
                .Where(rule => rule.Status == RequirementCalculationStatus.Calculated && rule.ResourceMatch == ResourceMatchStatus.Matched)
                .ToDictionary(rule => rule.ResourceId!.Value);
            var checks = stocks.Select(stock =>
            {
                var check = new ResourceCheckResponse(stock.InventoryStockId, stock.ResourceId, stock.ResourceName, stock.Unit, stock.AvailableQuantity, stock.AvailableQuantity <= stock.LowStockThreshold);
                return comparable.TryGetValue(stock.ResourceId, out var rule)
                    ? check with
                    {
                        Requested = rule.RequiredQuantity,
                        Sufficient = stock.AvailableQuantity >= rule.RequiredQuantity,
                        RequirementStatus = stock.AvailableQuantity >= rule.RequiredQuantity ? ResourceRequirementStatus.Sufficient : ResourceRequirementStatus.Insufficient
                    }
                    : check;
            }).ToList();

            var output = new WeatherResourceOutput(input.WorkflowId, "Analyzed", true, [], "Medium", "Summary from the forecast tool.", checks,
                ["Review stock before planting."], assessments, overall, requirements.Source, "Tool-based assessment.",
                [WeatherResourceToolNames.GetCropResourceRequirements, WeatherResourceToolNames.GetResourceAvailability, WeatherResourceToolNames.GetWeatherForecast]);
            return tamper?.Invoke(output) ?? output;
        }

        private async Task RecordAsync(WeatherResourceInput input, string toolName, object toolInput, object toolOutput)
        {
            db.AgentToolExecutions.Add(new AgentToolExecution
            {
                AgentStepId = input.AgentStepId,
                ToolName = toolName,
                InputJson = JsonSerializer.Serialize(toolInput, Json),
                OutputJson = JsonSerializer.Serialize(toolOutput, Json),
                Status = AgentToolExecutionStatus.Completed
            });
            await db.SaveChangesAsync();
        }
    }
    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")))
                Skip = "Disposable PostgreSQL connection is required.";
        }
    }
}
