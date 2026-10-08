# Member 4 Rice approval recovery evidence (draft for Agricultural Officer)

Status: **Unverified draft. Do not activate or use for a live approval.** Recorded 2026-10-07.

## Existing blocked workflow

- Workflow `a0077749-2763-4ed9-b504-8072183abc9e` remains `MissingDependency`, candidate revision 1. It plans Rice, variety Bg 352, in Anuradhapura, with a 2026-10-14 to 2027-02-09 window.
- Member 3 pinned profile `cb10d52b-5120-4b68-972c-2bc1db13ab52`. It is active and has one resource requirement but **zero growth stages**. Its 50 kg/ha Urea rule quotes a Guimba, Philippines example from an FAO primer, so it is not verified as a Sri Lankan Bg 352 recommendation.
- Another active Bg 352 profile, `8d1a4c7c-c72b-4ab0-9732-5ec788fbd76d`, has one Maturity stage (98–102 days) and **zero resource rules**. Combining the two profiles automatically would break the pinned-source handoff.
- No attempt should approve or retry this old candidate. A new workflow must be evaluated after a compatible profile is verified.

## Primary source findings for a new profile

1. Sri Lanka Department of Agriculture, RRDI [Bg 352 variety page](https://doa.gov.lk/rrdi_rice_bg352/): maturity is 98–102 days and the variety is recommended for general cultivation. This supports one *Maturity* milestone, not invented intermediate stage durations.
2. RRDI [rice varieties](https://doa.gov.lk/rrdi_rice_varities/) places Bg 352 in the 3½ month group.
3. RRDI [irrigated intermediate/dry-zone recommendation](https://doa.gov.lk/rrdi_fertilizerrecomendation_irrigated_izdz/) includes Anuradhapura. For a 3½ month crop, it lists total Urea 225, TSP 55, MOP 60, and zinc sulphate 5 kg/ha, with application timing in the source table.
4. RRDI [rainfed intermediate/dry-zone recommendation](https://doa.gov.lk/rrdi_fertilizerrecomendation_rainfed_izdz/) also includes Anuradhapura but lists different totals: Urea 175, TSP 35, MOP 50, and zinc sulphate 5 kg/ha. **The two sets are alternatives, not interchangeable.**

## Officer verification required before activation

- Confirm whether the actual field is irrigated or rainfed; the current Field and CropPlanRequest records do not encode this. Check field conditions and any current soil-test or official adjustment before applying a general table.
- Confirm that the profile's source URLs, maturity milestone, selected resource rates, units, and inventory resource IDs match the intended field and the approved local advice. The application stores a profile-level source URL, so each structured rule should also identify its specific RRDI page in its evidence notes.
- Confirm how the total rates and application timing should be represented. A total ResourceRequirement reservation does not itself create the source's split fertilizer schedule.
- Create a **new** inactive Bg 352 / Anuradhapura profile only after obtaining a source-supported timetable of successive stage durations and selecting the applicable resource rules. The current scheduler interprets stage days as durations, starting the first stage at planting. The 98–102 day maturity age is a milestone from planting and cannot be used as a lone stage duration; doing so would place the maturity task at planting. If only that milestone is available, preserve the block until a separate timing-model change is reviewed. The Agricultural Officer reviews the complete persisted profile before activation. Record who verified it and when.
- Re-run Member 1–3 on a new future-dated plan and check that Member 3 pins this new profile ID. Member 4 must consume the same ID and produce a reviewable candidate. Approval remains a separate explicit officer decision.

The current Guimba rule must not be treated as a locally applicable rate merely because its profile is marked active.


## Implemented recovery path and local verification — 2026-10-08

This is implementation and synthetic execution evidence. It does not verify the live Bg 352 field or authorize its approval.

- New reference versions are inactive drafts. Agricultural Officer verification is a separate server-recorded action containing officer ID, UTC time, field water regime, observation and expected draft version. The same officer may prepare and verify. Admin cannot substitute for this action.
- Draft editing uses optimistic concurrency. Verified content is immutable; corrections create a new draft. Legacy rows remain `LegacyReviewRequired` and do not become officer-verified during migration.
- Draft creation and editing preserve separate stage and rule source names/URLs; the web form can review and change these citations without replacing them with the profile overview. A focused regression test reproduced the original citation loss before the fix.
- The officer resolution page exposes persisted blocking reasons, sources, stage durations, resource values, verification state and the next responsible role. A rules-only profile cannot be verified. The stage timetable warning explains why the RRDI maturity milestone alone cannot fill this dependency.
- An Admin can start one replacement of the latest blocked workflow on the same future-dated request. Replacement ID, profile ID/version and idempotency key are persisted. Member 3 tools resolve the pin server-side; Member 4 rejects a different source or a legacy scheduling output. The superseded workflow remains readable and cannot be regenerated or approved.
- After approval, the web review shows guide Ready/Pending/Unavailable, generation date, refresh and officer retry. Existing approved work remains available. The farmer status/refresh integration from PR #87 is reused.

### Executed checks

| Check | Actual result |
| --- | --- |
| Backend restore and Release build | Passed; final build 0 warnings and 0 errors |
| Full xUnit suite with local PostgreSQL enabled | 390 passed, 0 failed, 0 skipped; `member4-verification-final.trx` |
| Final changed officer checks and integrated recovery rerun | 10 passed after the final handoff-label change |
| EF model versus migrations | No pending model changes; EF emits enum-default warnings, distinct from the clean build |
| PostgreSQL migration/verification/recovery focus | 3 passed: legacy upgrade and rollback refusal, concurrent draft edit versus verification, integrated recovery |
| AI service dependency install, compile and pytest | 229 passed; two dependency deprecation warnings |
| React clean install, lint, build and full Vitest | 145 passed across 20 files; lint has warnings, build reports existing bundle-size warning |
| Final clarified officer screen checks | Build passed and 12 affected React tests passed |
| Flutter pub get, analyze and tests | Analyze: no issues; 69 tests passed; generated lockfile changes restored |
| Browser against the local API and disposable PostgreSQL | Officer login, persisted recovery details, replacement link, approved guide Ready/date and refresh confirmed |
| Responsive web checks | Recovery and approved review checked at 390×844, desktop at 1280×900; phone document width matched viewport width, no horizontal overflow; override reset |

Commands used: the repository-required restore/build/test commands, `AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING` pointing only to local port 55432, AI `.venv` Python 3.12 compile/pytest, `npm ci`, `npm run lint`, `npm run build`, `npm test`, and Flutter pub get/analyze/test. Final focused tests selected `Member4RecoveryPostgreSqlTests` and `CropReferenceVerificationTests`. PostgreSQL migration tests also work with the isolated CI database on port 5432.

The integrated proof uses real member orchestration, read-only Member 3 tools and persisted snapshots, scheduling validation, the approval transaction, final-guide persistence and farmer ownership checks. AI and weather providers are deterministic synthetic fixtures; this is not a live model/provider demonstration. It asserts matching Member 3/4 profile IDs, no work before approval, two final tasks, one irrigation schedule, one reservation, guide Ready/date, denied access for a different farmer, unchanged old workflow and refused rollback that would erase recovery history.

Final passing synthetic run:

- Disposable database: `agriassist_member4_demo_verification_20261008` on localhost:55432.
- Plan: `11ef4bb2-35a2-4eb5-b301-bff69dd17d82`.
- Preserved blocked workflow: `86e81a3e-44bd-4b6f-8c21-5bc0626de05f`.
- New approved workflow: `45fdab62-f874-415f-8c3a-09e2b5ab6aa6`.
- Synthetic verified profile: `47829694-c851-4c6e-ab50-45d5edd10f0c`.
- Officer decision: explicit synthetic approval recorded by the test account, not a real agronomic decision.

### Remaining live rollout gate

1. Review PR #87 and the stacked evidence-verification PR. Do not merge before project review.
2. Back up and inspect the actual deployed database and migration history before applying the two new verification/replacement migrations and any missing final-guide uniqueness migration. Downgrade refuses to erase unverified drafts or pinned recovery history.
3. Deploy reviewed API/web changes and the compatible tested AI commit; confirm exact deployed commit IDs and protected routes. No production deployment or production database write was performed for this local proof.
4. A genuine Agricultural Officer must verify the actual Anuradhapura water regime, applicable RRDI rates and a source-supported timetable of successive stage durations. The project owner's plan approval and a clicked confirmation do not supply missing field/source evidence.
5. Only then start a new replacement run, complete Field Officer and Resource Officer handoffs, obtain a separate approval decision, and confirm final work plus guide through the deployed farmer API and Flutter. Live approval and live guide delivery remain unverified.

Implementation review: [PR #88](https://github.com/kaushalye1234/Intelligent-Pre-Cultivation-Farm-Planning-and-Management-System/pull/88), stacked on [PR #87](https://github.com/kaushalye1234/Intelligent-Pre-Cultivation-Farm-Planning-and-Management-System/pull/87). Local verification does not stand in for remote CI or production verification.
