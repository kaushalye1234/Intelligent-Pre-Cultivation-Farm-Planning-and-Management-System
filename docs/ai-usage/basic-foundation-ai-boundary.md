# AI Usage Boundary

This prompt builds the BASIC foundation only.

Implemented now:

- AgentWorkflow, AgentStep, AgentToolExecution, and AgentValidationResult persistence schema
- ApprovalDecision optional link to AgentWorkflow
- `IAgenticAIClient` placeholder service

Not implemented now:

- No prompt execution
- No LLM calls
- No autonomous tool execution
- No generated agronomy recommendations from AI

Future AI phases should treat these models as integration points and must keep backend-side validation and approval controls.