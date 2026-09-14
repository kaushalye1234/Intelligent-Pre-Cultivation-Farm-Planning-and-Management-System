using AgriAssist.Api.ExternalServices.Weather;

namespace AgriAssist.Api.Dtos.Resources;

/// <summary>A read-only copy of one inventory row, taken when the Member 3 step runs.</summary>
public sealed record StockSnapshot(
    Guid InventoryStockId,
    Guid ResourceId,
    string ResourceName,
    string Unit,
    decimal QuantityOnHand,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal LowStockThreshold);

/// <summary>Sent to the AI service. All evidence is gathered by ASP.NET so the agent needs no tool calls.</summary>
public sealed record WeatherResourceInput(
    Guid WorkflowId,
    Guid AgentStepId,
    Guid CropPlanRequestId,
    string Location,
    DateOnly PreferredStartDate,
    DateOnly PreferredEndDate,
    string FieldPriority,
    string FieldAnalysisSummary,
    WeatherForecastResponse Weather,
    IReadOnlyList<StockSnapshot> Stocks);

public sealed record ResourceCheckResponse(
    Guid InventoryStockId,
    Guid ResourceId,
    string ResourceName,
    string Unit,
    decimal AvailableQuantity,
    bool IsLowStock);

/// <summary>Member 3 output, stored in the WeatherResourceAnalysis AgentStep and read by Member 4.</summary>
public sealed record WeatherResourceOutput(
    Guid WorkflowId,
    string Status,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings,
    string WeatherRisk,
    string WeatherSummary,
    IReadOnlyList<ResourceCheckResponse> ResourceChecks,
    IReadOnlyList<string> Recommendations);

public sealed record WeatherResourceRunResponse(
    Guid WorkflowId,
    Guid CropPlanRequestId,
    Guid WeatherResourceStepId,
    string Status,
    string WeatherRisk,
    bool RequiresHumanReview,
    IReadOnlyList<string> Warnings);
