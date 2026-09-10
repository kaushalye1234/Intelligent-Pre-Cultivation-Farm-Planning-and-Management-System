# Member AI Readiness

The BASIC foundation is intended to support four later AI member contributions. Real AI remains out of scope until those prompts.

## Member 1 - CropPlanning AI

Build on CropPlanRequest, CropPlanRequestHistory, CropType, Farm, Field, and AgentWorkflow.

## Member 2 - Inspection AI

Build on FieldInspection, CropIssue, FollowUpRecommendation, InspectionImage, and AgentWorkflow.

## Member 3 - Resource AI

Build on Resource, InventoryStock, StockTransaction, ResourceReservation, and AgentWorkflow.

## Member 4 - TaskApproval AI

Build on FarmTask, IrrigationSchedule, ApprovalDecision, and optional AgentWorkflow references.

## Guardrail

Do not bypass manual approval, RBAC, validation, or backend service rules when adding AI.