# Field Analysis Response Schema Design

## Goal

Prevent `CropFieldAnalysisAgent` from accepting or requesting provider output that diverges from the existing Python and ASP.NET field-analysis contract, while preserving safe failure, evidence validation, retry behavior, permissions, and workflow sequencing.

## Approved approach

- Use `CropFieldAnalysisOutput.model_json_schema(by_alias=True)` as the structured response schema supplied to the configured LLM provider.
- Keep JSON parsing and Pydantic validation at the agent boundary. Do not normalize invalid enum values or coerce nested objects into strings.
- Make the Python response model match the existing ASP.NET contract: `status` is `Analyzed | SafeFailure`, `fieldCondition.summary` is required, and unknown response properties are rejected.
- Make the prompt state the exact output types and enum values and explicitly forbid alternate nested shapes such as `overallStatus`.
- Preserve the deterministic evidence checks after schema validation. The LLM must return the evidence-derived priority; it cannot choose a different priority.
- Log only structured validation diagnostics (error location, type, and safe message). Do not log the raw provider response, prompt, evidence, credentials, tokens, or user-entered notes.

## Provider interface

`BaseLLMProvider.generate_json` accepts an optional JSON-schema dictionary. Gemini dereferences the Pydantic `$defs` and maps it to the installed SDK's supported schema subset before passing it as `response_schema` together with `response_mime_type = application/json`; Pydantic remains the authoritative post-response validator for constraints the SDK cannot express. OpenAI uses JSON Schema structured output rather than JSON-object-only mode. Test providers accept the same optional argument.

## Error handling

Provider transport errors remain SafeFailure. JSON decoding, schema violations, unknown fields, and evidence conflicts also remain SafeFailure. The user-facing warning stays concise. Sanitized structured validation details are emitted to the AI-service logger for diagnosis without provider content.

## Testing

- Unit-test provider schema forwarding.
- Unit-test exact valid output and each observed invalid shape.
- Verify empty arrays remain valid and `FloodingRisk` retains evidence-derived `High` priority.
- Run focused and full AI tests.
- Run focused backend workflow/contract tests and the full backend suite if practical.
- React is not modified; run the relevant workflow-review regression suite to confirm integration presentation remains intact.
