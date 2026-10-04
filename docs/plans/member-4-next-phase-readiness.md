# Member 4 submission-phase readiness

Updated 2026-10-05 for Group 04. The core scheduling agent, candidate validation, officer approval, React review, Flutter farmer status, and PostgreSQL integration remain merged into `dev`. A clean follow-up branch, `member4/render-phase4-clean`, is based on latest fetched `origin/dev` (`bb3bd9d`) and locally contains the optional, disabled-by-default single-tool profile retrieval feature and Render demo configuration. The branch is pushed and open as draft PR #67; all four GitHub Actions jobs passed on commit `b5a6a29`. It is not merged or deployed.

## Verified for Member 4

- Full backend suite: 197 passed, 0 skipped against disposable PostgreSQL; AI: 86 passed; React: 94 passed; Flutter: 42 passed on the PR #59 head.
- A prior disposable API workflow reached approval, and a concurrency probe returned one HTTP 200 and one HTTP 409; farmer-scoped final records were checked. See the verification log for the fixtures and limits.
- A new Android debug APK was built from merged `dev` in 286.8 seconds on 2026-10-01. Its package/signature were checked, and it installed and launched to the login screen on the `Medium_Phone_API_36.1` emulator. The artifact and SHA-256 are recorded in the verification log.
- A Group 04 report draft and Member 4 evidence section are checked in as Markdown. A six-page review PDF was rendered locally and visually inspected. The draft is not the final single-PDF submission.
- The optional ReAct retrieval implementation is on the clean follow-up branch. The full AI suite passed 186 tests in an isolated Python 3.14 compatibility environment; the committed CI runtime is Python 3.12 with different pinned Pydantic version, so CI must verify the exact locked environment before merge.

## Group work still needed before submission

1. A Render demo blueprint, API container, PostgreSQL URL adapter, optional production Swagger switch, and deployment runbook are now prepared locally and YAML-checked. The group must review and sync the blueprint from `dev`, provide an OpenAI key/model and any optional service credentials, create the Render resources, then record public React, health, and Swagger URLs, migration evidence, and evaluator access. This group has not supplied deployed URLs yet.
2. Record and share a 10-minute demonstration video, then check access in a private browser. No video link has been supplied.
3. Have Members 1-3 provide and review their own contribution sections, key commits/tests, dated AI usage logs, approximately one-page personal reflections, and signed declarations. Member 4 must also write their own reflection and sign their own declaration. Do not fabricate these.
4. Combine the reviewed group report, all four individual sections, ADRs, diagrams, links, and AI declaration into **one** Group 04 PDF. Use the checked-in draft as an assembly source, then verify the final rendered PDF and every link.
5. Extend the single-fixture workflow timing evidence only if the group wants percentile or performance claims. The existing report correctly labels its sample count as one and makes no p50/p95 claim.

The Android APK and draft report address the local artifact gap. They do not establish cloud deployment, a live external LLM run, or final group authorship. The [Phase 3 checklist](../testing/phase3-final-submission-checklist.md) tracks exact verified results and limits.
