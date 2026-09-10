# ADR 0003: Disabled Agentic AI Client

## Decision

Create AgentWorkflow persistence and an `IAgenticAIClient` placeholder, but do not execute any LLM calls in this prompt.

## Status

Accepted.

## Consequences

Approval records can reference future workflows, while current behavior stays deterministic and non-AI.