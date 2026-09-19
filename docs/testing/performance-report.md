# AgriAssist Phase 3 performance report

## Scope

This report records measured local timings for one complete Member 4 workflow fixture. It is an integration smoke measurement, not a load test or a production capacity claim.

Environment: Windows development checkout, ASP.NET API on `127.0.0.1:5087`, AI service in Docker on port `8001`, PostgreSQL in Docker on port `55432`, deterministic AI fallback with no configured LLM provider key.

## Persisted workflow timings

Workflow: `d807e840-1c3a-4760-9055-6972fc429b80`.

| Step | Start (UTC) | Completed (UTC) | Duration |
|---|---:|---:|---:|
| Crop planning coordinator | 03:35:31.966522 | 03:35:32.624989 | 658.5 ms |
| Field analysis | 03:35:46.582363 | 03:35:47.287696 | 705.3 ms |
| Weather/resource analysis | 03:35:47.484866 | 03:35:47.548806 | 63.9 ms |
| Scheduling validation | 03:35:58.465018 | 03:35:58.613069 | 148.1 ms |

These values are taken from the persisted `AgentStep` timestamps returned by the workflow review API. The sample count is one per step; p50 and p95 are therefore not reported. A larger repeated run is required before using percentile targets.

## Approval and persistence evidence

- Candidate revision: 1
- Concurrent approval requests: 2
- Successful decision: 1 HTTP 200
- Competing decision: 1 HTTP 409
- Final approved tasks: 1
- Final approved irrigation schedules: 1
- Approval decisions: 1
- Final workflow state: `Completed`, version 4
- Farmer-scoped reads: one approved task, one approved irrigation schedule, and one workflow decision visible

## Limitations

The local fixture used deterministic fallback output. Weather provider access was unavailable and no stored inspection or inventory rows were present, so the workflow retained human-review warnings. No k6 load run or APK build completion is claimed by this report.
