# Disable Demo Crop Seeding Design

## Context

Backend startup currently invokes `SeedData.SeedAsync`, which inserts Rice, Maize, and Vegetables whenever `CropTypes` is empty. Administrators need an empty crop catalog to remain empty so they can enter real reference data manually.

## Decision

Remove the demo-only `SeedData` class and its startup invocation. Keep the in-memory `EnsureCreatedAsync` startup behavior because integration tests rely on the in-memory schema being initialized.

No replacement seed data, feature flag, migration, or schema change is needed. `SeedData` contains no unrelated seed behavior, so deleting it does not remove another startup responsibility.

## Verification

Add an integration test that starts the application in the `Testing` environment and verifies that `CropTypes`, `CropVarieties`, and `CropReferenceProfiles` are all empty. Run the targeted test, the Release build, and the complete backend test project before committing.
