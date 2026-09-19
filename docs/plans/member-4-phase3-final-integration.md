# Member 4 Phase 3: Final integration and submission evidence

Status: started on 2026-09-19 from merged `dev`.

Phase 2 established the scheduling-validation agent, candidate validation, officer approval, optimistic workflow version checks, and Flutter farmer status. Phase 3 packages the integrated behavior into repeatable evidence and closes the remaining release-readiness gaps.

## Work sequence

1. Preserve the verified end-to-end fixture and record the exact backend, AI, React, Flutter, Docker, PostgreSQL, and .NET versions.
2. Keep the API concurrency probe repeatable and include one successful decision plus one HTTP 409 result in the verification log.
3. Verify farmer-scoped visibility of the approved task, irrigation schedule, and approval history.
4. Measure workflow step timings and report sample counts and limitations. Do not claim p50/p95 from a single run.
5. Run backend, AI-service, React, and Flutter checks from a clean checkout. Record APK status accurately.
6. Review Swagger, ERD, ADRs, startup instructions, CI workflow, and environment-name consistency.
7. Run the final submission checklist and open a review PR into `dev`.

## Acceptance evidence

- `dotnet test` passes for the backend test project.
- AI-service pytest, React build/tests, and Flutter analyze/tests are recorded with their actual results.
- A real local workflow reaches `CandidateReady`, and approval creates final records atomically.
- Concurrent approval produces one success and one HTTP 409 conflict.
- Farmer reads are scoped to the owning farmer and show final status/history.
- Performance results identify the target, sample count, timings, and unavailable measurements.
- No credentials, logs, build output, or generated APK claims are committed without evidence.
