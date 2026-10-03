# Member 4 submission-phase readiness

Updated 2026-10-01 for Group 04. The implementation phases are complete: the scheduling agent, candidate validation, officer approval, React review, Flutter farmer status, and PostgreSQL integration have been merged into `dev`. PR #59 passed backend, AI service, React, and Flutter CI before merge commit `46c50b4`.

## Alignment update — 2026-10-03

The approved single-tool ReAct design is a separate Member 4 follow-up: the AI may request one verified crop profile through an existing read-only tool, then the deterministic scheduler builds the proposal. The implementation is not yet started.

This addition does not reopen or change the completed Phase 1–3 implementation and evidence. The deployment, demo video, and consolidated report remain separate Group 04 submission work. ReAct is not a deployment prerequisite; include it in the demo only if it is implemented, reviewed, and tested before the deployment freeze. The focused implementation plan is [here](../superpowers/plans/2026-10-03-member4-readonly-react.md), and the approved design is [here](../superpowers/specs/2026-10-03-member4-readonly-react-design.md).

## Verified for Member 4

- Full backend suite: 197 passed, 0 skipped against disposable PostgreSQL; AI: 86 passed; React: 94 passed; Flutter: 42 passed on the PR #59 head.
- A prior disposable API workflow reached approval, and a concurrency probe returned one HTTP 200 and one HTTP 409; farmer-scoped final records were checked. See the verification log for the fixtures and limits.
- A new Android debug APK was built from merged `dev` in 286.8 seconds on 2026-10-01. Its package/signature were checked, and it installed and launched to the login screen on the `Medium_Phone_API_36.1` emulator. The artifact and SHA-256 are recorded in the verification log.
- A Group 04 report draft and Member 4 evidence section are checked in as Markdown. A six-page review PDF was rendered locally and visually inspected. The draft is not the final single-PDF submission.

## Group work still needed before submission

1. Deploy the React app, ASP.NET API, and PostgreSQL; record public React, health, and Swagger URLs, migration evidence, and evaluator access. This group has not supplied deployed URLs yet.
2. Record and share a 10-minute demonstration video, then check access in a private browser. No video link has been supplied.
3. Have Members 1-3 provide and review their own contribution sections, key commits/tests, dated AI usage logs, approximately one-page personal reflections, and signed declarations. Member 4 must also write their own reflection and sign their own declaration. Do not fabricate these.
4. Combine the reviewed group report, all four individual sections, ADRs, diagrams, links, and AI declaration into **one** Group 04 PDF. Use the checked-in draft as an assembly source, then verify the final rendered PDF and every link.
5. Extend the single-fixture workflow timing evidence only if the group wants percentile or performance claims. The existing report correctly labels its sample count as one and makes no p50/p95 claim.

The Android APK and draft report address the local artifact gap. They do not establish cloud deployment, a live external LLM run, or final group authorship. The [Phase 3 checklist](../testing/phase3-final-submission-checklist.md) tracks exact verified results and limits.
