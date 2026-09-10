# ADR 0008: Workflow-State Database Strategy

## Decision

Persist future AI workflow state in relational tables: AgentWorkflow, AgentStep, AgentToolExecution, and AgentValidationResult.

## Status

Accepted.

## Consequences

Future Agentic AI work can attach execution state to existing manual business workflows without changing the core operational tables.