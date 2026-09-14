# Crop Planning AI Coordinator Design

Date: 2026-09-13
Status: Approved and implemented

## Context

Member 1 owns the shared Python AI service scaffold and the crop planning coordinator. Prompt 01 already provides ASP.NET Core, React, Flutter, RBAC, crop planning records, and AgentWorkflow persistence. This design extends that base without rebuilding it.

## Architecture

ASP.NET remains the only public application API. React and Flutter call ASP.NET endpoints only. ASP.NET starts crop planning workflows, persists AgentWorkflow and AgentStep state, validates coordinator responses, and exposes read-only internal tool endpoints for the AI service under `/api/internal/agent-tools`.

The Python `ai-service` is a separate FastAPI service with LangGraph, Pydantic schemas, an LLM provider abstraction, and a single crop planning coordinator endpoint. It uses `AI_SERVICE_TOKEN` for incoming ASP.NET calls and `BACKEND_TOOL_TOKEN` for read-only backend tool calls.

## Coordinator Behavior

`CropPlanningCoordinatorAgent` gathers minimum plan context through allow-listed tools, checks verified crop reference availability, summarizes the farmer objective, and returns a structured envelope. Downstream delegation is fixed to:

1. `CropFieldAnalysisAgent`
2. `WeatherResourceAgent`
3. `SchedulingValidationAgent`

The coordinator cannot approve, reserve stock, create final tasks, run SQL, or invent crop facts. Missing reference data and provider/tool failures return safe status envelopes.

## Data Flow

Farmer or staff creates a crop plan request. Starting AI planning creates `AgentWorkflow` and the coordinator `AgentStep`, calls the AI service, validates the response, stores coordinator output JSON, adds downstream pending steps on success, and returns the workflow ID/status.

## Error Handling

Malformed AI output, provider timeout, unavailable service, or missing reference data never produces fabricated crop facts. ASP.NET persists a sanitized `SafeFailure` or `ReferenceDataUnavailable` result and records validation errors.

## Testing

Backend tests cover persistence, downstream step readiness, missing reference data, and AI service unavailability. Python tests cover golden output, invalid input, missing reference data, malformed LLM JSON, provider timeout, prompt injection, forbidden approval attempts, tool selection, and safe tool failure. React and Flutter tests cover the workflow status/result UI surfaces.
