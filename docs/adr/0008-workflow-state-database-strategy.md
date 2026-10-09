# ADR 0008: Workflow-State Database Strategy

## Decision

Persist AI workflow state in relational tables: AgentWorkflow, AgentStep, AgentToolExecution, and AgentValidationResult. Link officer decisions and generated business records to the workflow and candidate revision for provenance.

## Status

Accepted.

## Consequences

The implemented four-agent workflow attaches execution and validation summaries to crop-plan requests. The backend uses workflow version and candidate revision checks before officer approval; only approved execution creates linked tasks, irrigation schedules, and resource reservations. These records preserve auditability while manual business records remain supported. The workflow tables store structured results and tool summaries, not hidden model reasoning or credentials.
