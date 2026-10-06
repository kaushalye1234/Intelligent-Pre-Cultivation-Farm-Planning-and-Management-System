# Member 4 scheduling verification — 2026-10-06

This records a focused local check of the scheduling proposal and officer approval gates. The tests ran in the main checkout at commit `70e9110`, which also had uncommitted changes. This document is committed separately from the current `dev` tip; its results do not certify a clean `dev` build or a successful end-to-end approval.

| Check | Result |
| --- | --- |
| Local API `http://127.0.0.1:5087/health` | HTTP 200 |
| Local AI service `http://127.0.0.1:8001/health` | HTTP 200 |
| Local React app `http://127.0.0.1:5173/` | HTTP 200 |
| Focused AI scheduling agent, HTTP contract, and graph tests | 16 passed |
| Focused backend `WorkflowApprovalTests` | 18 passed, 3 skipped |
| React scheduling proposal parser tests | 3 passed |

Commands used:

```powershell
cd ai-service
.\.venv\Scripts\python.exe -m pytest tests/test_scheduling_validation_agent.py tests/test_scheduling_validation_api.py tests/test_scheduling_validation_graph.py -q

cd ..
dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~WorkflowApprovalTests --verbosity quiet -p:OutDir=artifacts/test-isolated/

cd frontend/react-app
npm.cmd test -- src/pages/schedulingProposal.test.ts
```

The separate .NET output directory avoided a Windows DLL lock held by the running API. The temporary test output was removed after the run. NuGet reported that it could not reach its vulnerability-data index, but the focused backend tests completed. The three skipped tests were not counted as verified behavior.

An authenticated, read-only API check of workflows `6746aaab-52bb-4d14-83f1-1d7bc3ff5d78` and `a0077749-2763-4ed9-b504-8072183abc9e` returned `MissingDependency`. Each stored scheduling output had zero candidate tasks and warned: “A verified crop profile with stages is required.” `SchedulingCandidateValidator` rejected approval because the candidate was not ready, had no tasks, and lacked a matching active verified crop profile with stages. Workflow `5872f587-8d72-45be-9bff-bdd3a1328276`, shown in an earlier screenshot, now reports `Cancelled`.

These results demonstrate the safe block for incomplete upstream evidence. They do not demonstrate candidate generation, PostgreSQL approval transaction behavior, or successful human approval. A successful demonstration needs a matching active verified crop profile with stages and compatible resource evidence, followed by a new workflow using an appropriate planning window. No candidate was generated or approved during this verification.
