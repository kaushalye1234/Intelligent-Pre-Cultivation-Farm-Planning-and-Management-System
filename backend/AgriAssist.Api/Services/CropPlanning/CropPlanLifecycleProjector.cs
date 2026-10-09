using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Services.CropPlanning;

internal sealed record CropPlanLifecycleProjection(
    string StatusCode,
    string StatusLabel,
    string OverallStatusCode,
    string OverallStatusLabel);

internal static class CropPlanLifecycleProjector
{
    internal const string Pending = "pending";
    internal const string AiPlanning = "ai_planning";
    internal const string AiPlanningFailed = "ai_planning_failed";
    internal const string PreliminaryPlanReady = "preliminary_plan_ready";

    private const string CoordinatorAgent = "CropPlanningCoordinatorAgent";
    private const string FieldAnalysisAgent = "CropFieldAnalysisAgent";
    private const string WeatherResourceAgent = "WeatherResourceAgent";
    private const string SchedulingAgent = "SchedulingValidationAgent";

    internal static CropPlanLifecycleProjection Project(CropPlanRequestStatus requestStatus, AgentWorkflow? workflow)
    {
        var final = requestStatus switch
        {
            CropPlanRequestStatus.Approved => Stage("approved", "Approved", "approved", "Approved"),
            CropPlanRequestStatus.Rejected => Stage("rejected", "Rejected", "rejected", "Rejected"),
            CropPlanRequestStatus.Cancelled => Stage("cancelled", "Cancelled", "cancelled", "Cancelled"),
            _ => null
        };
        if (final is not null) return final;

        if (workflow is null)
        {
            return requestStatus == CropPlanRequestStatus.PreliminaryGenerated
                ? InProgress(PreliminaryPlanReady, "Preliminary Plan Ready")
                : InProgress(Pending, "Pending");
        }

        var coordinator = Step(workflow, CoordinatorAgent);
        if (requestStatus == CropPlanRequestStatus.Submitted)
        {
            if (coordinator?.Status == AgentStepStatus.Running
                || (workflow.Status == AgentWorkflowStatus.Running && workflow.CurrentStep == CoordinatorAgent))
            {
                return InProgress(AiPlanning, "AI Planning");
            }

            if (coordinator?.Status is AgentStepStatus.Failed or AgentStepStatus.Completed
                || workflow.Status == AgentWorkflowStatus.Failed)
            {
                return InProgress(AiPlanningFailed, "AI Planning Failed — awaiting Admin retry");
            }

            return InProgress(Pending, "Pending");
        }

        var workflowState = workflow.Status switch
        {
            AgentWorkflowStatus.CandidateReady => InProgress("candidate_ready", "Candidate Plan Ready"),
            AgentWorkflowStatus.PendingOfficerApproval => InProgress("awaiting_approval", "Awaiting Final Approval"),
            AgentWorkflowStatus.RevisionRequested => InProgress("revision_requested", "Revision Requested"),
            AgentWorkflowStatus.MissingDependency => InProgress("waiting_for_required_data", "Waiting for Required Data"),
            AgentWorkflowStatus.CandidateBlocked => InProgress("candidate_blocked", "Candidate Plan Blocked"),
            AgentWorkflowStatus.Rejected => Stage("rejected", "Rejected", "rejected", "Rejected"),
            AgentWorkflowStatus.Cancelled => Stage("cancelled", "Cancelled", "cancelled", "Cancelled"),
            _ => null
        };
        if (workflowState is not null) return workflowState;

        var field = Step(workflow, FieldAnalysisAgent);
        var weather = Step(workflow, WeatherResourceAgent);
        var scheduling = Step(workflow, SchedulingAgent);

        if (workflow.CurrentStep == SchedulingAgent)
        {
            if (scheduling?.Status == AgentStepStatus.Running || workflow.Status == AgentWorkflowStatus.Running)
                return InProgress("scheduling_validation_running", "Scheduling Validation in Progress");
            if (scheduling?.Status == AgentStepStatus.Failed)
                return InProgress("scheduling_validation_failed", "Scheduling Validation Failed");
            return InProgress("awaiting_scheduling_validation", "Awaiting Scheduling Validation");
        }

        if (workflow.CurrentStep == WeatherResourceAgent)
        {
            if (weather?.Status == AgentStepStatus.Running || workflow.Status == AgentWorkflowStatus.Running)
                return InProgress("weather_resource_analysis_running", "Weather & Resource Analysis in Progress");
            if (weather?.Status == AgentStepStatus.Failed)
                return InProgress("weather_resource_analysis_failed", "Weather & Resource Analysis Failed");
            return InProgress("waiting_for_weather_resource_analysis", "Waiting for Weather & Resource Analysis");
        }

        if (workflow.CurrentStep == FieldAnalysisAgent)
        {
            if (field?.Status == AgentStepStatus.Running || workflow.Status == AgentWorkflowStatus.Running)
                return InProgress("field_analysis_running", "Field Analysis in Progress");
            if (field?.Status == AgentStepStatus.Failed)
                return InProgress("field_analysis_failed", "Field Analysis Failed");
            return InProgress(PreliminaryPlanReady, "Preliminary Plan Ready");
        }

        if (workflow.Status == AgentWorkflowStatus.Completed)
            return InProgress("workflow_complete", "Workflow Complete");
        if (workflow.Status == AgentWorkflowStatus.Failed)
        {
            if (scheduling?.Status == AgentStepStatus.Failed)
                return InProgress("scheduling_validation_failed", "Scheduling Validation Failed");
            if (weather?.Status == AgentStepStatus.Failed)
                return InProgress("weather_resource_analysis_failed", "Weather & Resource Analysis Failed");
            if (field?.Status == AgentStepStatus.Failed)
                return InProgress("field_analysis_failed", "Field Analysis Failed");
            return InProgress("workflow_failed", "Workflow Failed");
        }

        return InProgress(PreliminaryPlanReady, "Preliminary Plan Ready");
    }

    private static AgentStep? Step(AgentWorkflow workflow, string agentName) =>
        workflow.Steps
            .Where(step => !step.IsDeleted && step.AgentName == agentName)
            .OrderByDescending(step => step.Sequence)
            .ThenByDescending(step => step.CreatedAt)
            .FirstOrDefault();

    private static CropPlanLifecycleProjection InProgress(string code, string label) =>
        Stage(code, label, "in_progress", "In Progress");

    private static CropPlanLifecycleProjection Stage(string code, string label, string overallCode, string overallLabel) =>
        new(code, label, overallCode, overallLabel);
}
