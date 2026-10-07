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
- Create a **new** inactive Bg 352 / Anuradhapura profile with the verified Maturity stage and only the selected applicable rules. The Agricultural Officer reviews the complete persisted profile before activation. Record who verified it and when.
- Re-run Member 1–3 on a new future-dated plan and check that Member 3 pins this new profile ID. Member 4 must consume the same ID and produce a reviewable candidate. Approval remains a separate explicit officer decision.

The current Guimba rule must not be treated as a locally applicable rate merely because its profile is marked active.
