# ADR 0005: Agentic AI Framework and Orchestration

## Status

Accepted.

## Decision

Use a separate Python FastAPI service for shared AI orchestration, with LangGraph for workflow nodes, Pydantic for input/output contracts, and an OpenAI provider behind the shared LLM interface.

The AI service uses `AI_PROVIDER=openai`, `AI_MODEL=gpt-6-luna`, and the server-side `OPENAI_API_KEY`. Both Member 1 agents consume the same configured provider/model. Secrets remain runtime configuration and are never exposed to React or Flutter.

## Rationale

Python + FastAPI keeps AI orchestration, provider SDKs, and agent testing isolated from the ASP.NET business API. LangGraph gives explicit graph nodes and future multi-agent extension points while still allowing deterministic safety checks around each step. Pydantic gives strict JSON contracts for teammates and for backend validation.

The shared provider interface keeps agent code independent from SDK details while the deployed project remains OpenAI-only. Missing OpenAI configuration falls back to deterministic coordinator behavior rather than exposing secrets or fabricating facts.

## Security

ASP.NET calls the AI service with `AI__ServiceToken`, which must match `AI_SERVICE_TOKEN`. The AI service calls only allow-listed internal ASP.NET tool endpoints with `BACKEND_TOOL_TOKEN`, which must match `AI__ToolToken`. React and Flutter never receive database credentials, JWT signing secrets, Cloudinary secrets, provider keys, or internal-service tokens.

Internal tool endpoints are separated under `/api/internal/agent-tools`, validate tokens, return minimum structured data, enforce workflow context where relevant, and record `AgentToolExecution` entries.

## Failure Behavior

The coordinator returns structured safe envelopes. Missing verified crop reference data returns `ReferenceDataUnavailable` with `requiresHumanReview=true`. Provider timeout, malformed LLM JSON, forbidden action attempts, or tool failure returns `SafeFailure` with warnings. ASP.NET validates the envelope before persisting downstream steps.

## Training Decision

Custom model training is not required. The coordinator uses authoritative database/tool context, verified crop reference records, deterministic guardrails, and narrow LLM summarization. Fine-tuning would add cost and operational risk without solving the core safety requirement: the system must not invent crop facts or execute unapproved operations.
