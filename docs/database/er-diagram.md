# ER Diagram

```mermaid
erDiagram
    AppUsers ||--o{ Farms : owns
    AppUsers ||--o{ CropPlanRequests : requests
    AppUsers ||--o{ FieldInspections : inspects
    AppUsers ||--o{ FarmTasks : assigned
    AppUsers ||--o{ ApprovalDecisions : decides
    AppUsers ||--o{ ResourceReservations : requests

    Farms ||--o{ Fields : contains
    Farms ||--o{ CropPlanRequests : has
    Farms ||--o{ FarmTasks : has
    Fields ||--o{ CropCycles : grows
    Fields ||--o{ CropPlanRequests : scopes
    Fields ||--o{ FieldInspections : inspected
    Fields ||--o{ IrrigationSchedules : schedules
    CropTypes ||--o{ CropCycles : used_by
    CropTypes ||--o{ CropPlanRequests : requested
    CropPlanRequests ||--o{ CropPlanRequestHistory : records

    FieldInspections ||--o{ InspectionObservations : records
    FieldInspections ||--o{ CropIssues : finds
    FieldInspections ||--o{ InspectionImages : uploads
    CropIssues ||--o{ FollowUpRecommendations : recommends

    ResourceCategories ||--o{ Resources : groups
    Suppliers ||--o{ Resources : supplies
    Resources ||--o{ InventoryStocks : stocked
    InventoryStocks ||--o{ StockTransactions : logs
    InventoryStocks ||--o{ ResourceReservations : reserves

    AgentWorkflows ||--o{ AgentSteps : contains
    AgentWorkflows ||--o{ AgentToolExecutions : executes
    AgentWorkflows ||--o{ AgentValidationResults : validates
    AgentWorkflows ||--o{ ApprovalDecisions : supports
    CropPlanRequests ||--o{ AgentWorkflows : starts
    AgentWorkflows ||--o{ FarmTasks : generates
    AgentWorkflows ||--o{ IrrigationSchedules : generates
    AgentWorkflows ||--o{ ResourceReservations : generates
```

The workflow tables are active. `AgentStep` stores each agent's execution result, `AgentValidationResult` records candidate checks, and `ApprovalDecision` records the officer's decision. Final tasks, irrigation schedules, and reservations have nullable `GeneratedByWorkflowId` provenance and a candidate revision; they are created only after approval. The workflow-to-final-record relationships are optional because manual records also exist.
