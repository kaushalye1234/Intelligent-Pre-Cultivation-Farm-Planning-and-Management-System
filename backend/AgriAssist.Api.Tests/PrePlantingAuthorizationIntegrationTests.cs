using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Dtos.Inspections;
using AgriAssist.Api.Dtos.Shared;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Inspections;
using AgriAssist.Api.Models.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriAssist.Api.Tests;

/// <summary>HTTP-level checks for the raw Member 2 PrePlanting evidence boundary.</summary>
public sealed class PrePlantingAuthorizationIntegrationTests
{
    private const string Password = "Inspection@2026";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Farmer_and_resource_officer_cannot_read_raw_pre_planting_data_through_generic_apis()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);
        using var farmer = await ClientForAsync(factory, data.FarmerEmail);
        using var resourceOfficer = await ClientForAsync(factory, data.ResourceOfficerEmail);

        foreach (var client in new[] { farmer, resourceOfficer })
        {
            var inspections = await client.GetFromJsonAsync<PagedResult<FieldInspectionResponse>>("/api/inspections", Json);
            Assert.Contains(inspections!.Items, item => item.Id == data.RoutineInspectionId);
            Assert.DoesNotContain(inspections.Items, item => item.Id == data.PrePlantingInspectionId);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inspections/{data.PrePlantingInspectionId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inspections/{data.PrePlantingInspectionId}/history")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inspections/{data.PrePlantingInspectionId}/images")).StatusCode);

            var observations = await client.GetFromJsonAsync<PagedResult<ObservationResponse>>(
                $"/api/inspections/observations?inspectionId={data.PrePlantingInspectionId}", Json);
            Assert.Empty(observations!.Items);
            var issues = await client.GetFromJsonAsync<PagedResult<CropIssueResponse>>("/api/inspections/issues", Json);
            Assert.DoesNotContain(issues!.Items, item => item.Id == data.PrePlantingIssueId);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/inspections/issues/{data.PrePlantingIssueId}")).StatusCode);
            var recommendations = await client.GetFromJsonAsync<PagedResult<FollowUpRecommendationResponse>>(
                $"/api/inspections/recommendations?cropIssueId={data.PrePlantingIssueId}", Json);
            Assert.Empty(recommendations!.Items);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/crop-plans/{data.CropPlanRequestId}/pre-planting-assessment")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/crop-plans/{data.CropPlanRequestId}/field-analysis-result")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await farmer.GetAsync($"/api/crop-plans/{data.CropPlanRequestId}/member-3-handoff")).StatusCode);
        var safeResponse = await resourceOfficer.GetAsync($"/api/crop-plans/{data.CropPlanRequestId}/member-3-handoff");
        Assert.Equal(HttpStatusCode.OK, safeResponse.StatusCode);
        var safeJson = await safeResponse.Content.ReadAsStringAsync();
        Assert.Contains("fieldSuitability", safeJson, StringComparison.Ordinal);
        Assert.Contains("waterAssessment", safeJson, StringComparison.Ordinal);
        Assert.Contains("drainageAssessment", safeJson, StringComparison.Ordinal);
        Assert.Contains("plantingReadiness", safeJson, StringComparison.Ordinal);
        Assert.Contains("identifiedRisks", safeJson, StringComparison.Ordinal);
        Assert.Contains("fieldPreparationRequirements", safeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("officerNotes", safeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("riskNotes", safeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidenceInspectionIds", safeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openIssues", safeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inspectionImages", safeJson, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(HttpStatusCode.OK, (await resourceOfficer.GetAsync(
            $"/api/crop-plans/{data.UnrelatedCropPlanRequestId}/member-3-handoff")).StatusCode);
    }

    [Fact]
    public async Task Agricultural_officer_and_admin_have_read_only_raw_access_and_resource_officer_cannot_modify()
    {
        await using var factory = CreateFactory();
        var data = await SeedAsync(factory);
        using var agriculturalOfficer = await ClientForAsync(factory, data.AgriculturalOfficerEmail);
        using var admin = await ClientForAsync(factory, data.AdminEmail);
        using var resourceOfficer = await ClientForAsync(factory, data.ResourceOfficerEmail);
        var update = new FieldInspectionRequest(
            data.FieldId,
            DateTime.UtcNow,
            InspectionStatus.InProgress,
            "Unauthorized generic update.");

        foreach (var client in new[] { agriculturalOfficer, admin })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inspections/{data.PrePlantingInspectionId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inspections/{data.PrePlantingInspectionId}/history")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/inspections/{data.PrePlantingInspectionId}/images")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/inspections/{data.PrePlantingInspectionId}", update)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
                "/api/inspections/observations",
                new ObservationRequest(data.PrePlantingInspectionId, "OfficerNotes", "Unauthorized edit."))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/inspections/{data.PrePlantingInspectionId}/submit", null)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await resourceOfficer.PutAsJsonAsync(
            $"/api/inspections/{data.PrePlantingInspectionId}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await resourceOfficer.PostAsJsonAsync(
            "/api/inspections/observations",
            new ObservationRequest(data.PrePlantingInspectionId, "OfficerNotes", "Resource Officer edit."))).StatusCode);
        using var imageBody = new MultipartFormDataContent();
        Assert.Equal(HttpStatusCode.Forbidden, (await resourceOfficer.PostAsync(
            $"/api/inspections/{data.PrePlantingInspectionId}/images", imageBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await resourceOfficer.PutAsJsonAsync(
            $"/api/crop-plans/{data.CropPlanRequestId}/pre-planting-assessment", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await resourceOfficer.PostAsync(
            $"/api/crop-plans/{data.CropPlanRequestId}/pre-planting-assessment/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await resourceOfficer.PostAsync(
            $"/api/crop-plans/{data.CropPlanRequestId}/run-field-analysis", null)).StatusCode);
    }

    private static async Task<SeededAuthorizationData> SeedAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farmer = User(ApplicationRole.Farmer);
        var fieldOfficer = User(ApplicationRole.FieldOfficer);
        var resourceOfficer = User(ApplicationRole.ResourceOfficer);
        var agriculturalOfficer = User(ApplicationRole.AgriculturalOfficer);
        var admin = User(ApplicationRole.Admin);
        var farm = new Farm { Name = "Authorization Farm", Location = "North", TotalArea = 10, OwnerUser = farmer };
        var field = new Field { Name = "Authorization Field", Area = 2, SoilType = "Loam", Farm = farm, IsActive = true };
        var crop = new CropType { Name = "Rice", IsActive = true };
        var request = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2026, 10, 1),
            PreferredEndDate = new DateOnly(2027, 1, 1),
            Budget = 12000,
            Objective = "Verify raw assessment authorization.",
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        var unrelatedRequest = new CropPlanRequest
        {
            Farm = farm,
            Field = field,
            CropType = crop,
            RequestedByUser = farmer,
            PreferredStartDate = new DateOnly(2027, 2, 1),
            PreferredEndDate = new DateOnly(2027, 5, 1),
            Budget = 9000,
            Objective = "Unrelated crop plan still waiting for Member 2.",
            Status = CropPlanRequestStatus.PreliminaryGenerated
        };
        var routine = new FieldInspection
        {
            Field = field,
            InspectorUser = fieldOfficer,
            Purpose = InspectionPurpose.Routine,
            ScheduledAt = DateTime.UtcNow,
            Status = InspectionStatus.InProgress,
            Summary = "Routine inspection."
        };
        var prePlanting = new FieldInspection
        {
            Field = field,
            CropPlanRequest = request,
            InspectorUser = fieldOfficer,
            Purpose = InspectionPurpose.PrePlanting,
            ScheduledAt = DateTime.UtcNow,
            Status = InspectionStatus.InProgress,
            Summary = "Raw pre-planting assessment."
        };
        var routineObservation = new InspectionObservation { FieldInspection = routine, ObservationType = "Routine", Notes = "Routine note." };
        var prePlantingObservation = new InspectionObservation { FieldInspection = prePlanting, ObservationType = "OfficerNotes", Notes = "Private staff note." };
        var routineIssue = new CropIssue { FieldInspection = routine, Title = "Routine issue", Description = "Routine issue.", Severity = CropIssueSeverity.Low, Status = CropIssueStatus.Open };
        var prePlantingIssue = new CropIssue { FieldInspection = prePlanting, Title = "Pre-planting issue", Description = "Private issue.", Severity = CropIssueSeverity.High, Status = CropIssueStatus.Open };
        var recommendation = new FollowUpRecommendation { CropIssue = prePlantingIssue, Recommendation = "Private follow-up.", IsCompleted = false };
        var image = new InspectionImage
        {
            FieldInspection = prePlanting,
            Url = "https://images.test/preplant.jpg",
            PublicId = "preplant/private",
            ContentType = "image/jpeg",
            SizeBytes = 10
        };
        var workflow = new AgentWorkflow
        {
            CropPlanRequest = request,
            InitiatedByUser = farmer,
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = "WeatherResourceAgent"
        };
        var fieldAnalysis = new FieldAnalysisOutput(
            workflow.Id,
            "Analyzed",
            true,
            ["Human review remains required."],
            new FieldAnalysisFieldConditionResponse("Structured staff analysis summary.", [prePlanting.Id]),
            [new FieldAnalysisOpenIssueResponse(prePlantingIssue.Id, "High", "Open", prePlanting.Id)],
            "High",
            "SuitableWithConditions",
            "Soil type Loamy; condition Moderate; moisture Moist.",
            "Water availability Adequate; main source Canal; irrigation Available; reliability Reliable.",
            "Drainage condition Poor; waterlogging risk Moderate.",
            ["Clear drainage channels before planting."],
            "RequiresPreparation",
            [PrePlantingRisk.PoorDrainage],
            ["Address the drainage concern before planting."]);
        workflow.Steps =
        [
            new AgentStep
            {
                AgentName = "CropFieldAnalysisAgent",
                StepName = "FieldAnalysis",
                Sequence = 2,
                Status = AgentStepStatus.Completed,
                OutputJson = JsonSerializer.Serialize(fieldAnalysis, Json)
            },
            new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3 }
        ];
        var unrelatedWorkflow = new AgentWorkflow
        {
            CropPlanRequest = unrelatedRequest,
            InitiatedByUser = farmer,
            Objective = unrelatedRequest.Objective,
            Status = AgentWorkflowStatus.Pending,
            CurrentStep = "CropFieldAnalysisAgent",
            Steps =
            [
                new AgentStep { AgentName = "CropFieldAnalysisAgent", StepName = "FieldAnalysis", Sequence = 2, Status = AgentStepStatus.Pending },
                new AgentStep { AgentName = "WeatherResourceAgent", StepName = "WeatherResourceAnalysis", Sequence = 3 }
            ]
        };
        db.AddRange(farmer, fieldOfficer, resourceOfficer, agriculturalOfficer, admin, farm, field, crop, request,
            unrelatedRequest, routine, prePlanting, routineObservation, prePlantingObservation, routineIssue,
            prePlantingIssue, recommendation, image, workflow, unrelatedWorkflow);
        await db.SaveChangesAsync();

        return new SeededAuthorizationData(
            farmer.Email,
            resourceOfficer.Email,
            agriculturalOfficer.Email,
            admin.Email,
            field.Id,
            request.Id,
            unrelatedRequest.Id,
            routine.Id,
            prePlanting.Id,
            prePlantingIssue.Id);
    }

    private static AppUser User(ApplicationRole role) =>
        new()
        {
            FullName = $"Test {role}",
            Email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@inspection.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            Role = role,
            IsActive = true,
            MustChangePassword = false,
            PasswordChangedAt = DateTime.UtcNow,
            TokenVersion = 1
        };

    private static async Task<HttpClient> ClientForAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "test-jwt-secret-with-at-least-32-chars",
                        ["Jwt:Issuer"] = "AgriAssist",
                        ["Jwt:Audience"] = "AgriAssistUsers",
                        ["Jwt:ExpiryMinutes"] = "60",
                        ["Jwt:PasswordChangeExpiryMinutes"] = "10"
                    });
                });
            });

    private sealed record SeededAuthorizationData(
        string FarmerEmail,
        string ResourceOfficerEmail,
        string AgriculturalOfficerEmail,
        string AdminEmail,
        Guid FieldId,
        Guid CropPlanRequestId,
        Guid UnrelatedCropPlanRequestId,
        Guid RoutineInspectionId,
        Guid PrePlantingInspectionId,
        Guid PrePlantingIssueId);
}
