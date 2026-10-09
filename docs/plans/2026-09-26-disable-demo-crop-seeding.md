# Disable Demo Crop Seeding Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Stop backend startup from recreating demo crop types while preserving in-memory database initialization and all unrelated behavior.

**Architecture:** Remove the demo-only seed entry point rather than retaining dead no-op code or adding a configuration switch. Cover the startup boundary with an in-memory integration test that asserts the crop catalog and reference profile collections remain empty.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core InMemory, xUnit, `WebApplicationFactory<Program>`

---

### Task 1: Add the startup regression test

**Files:**
- Create: `backend/AgriAssist.Api.Tests/StartupDataSeedingTests.cs`

**Step 1: Write the failing test**

Create a `WebApplicationFactory<Program>` in the `Testing` environment, start it with `CreateClient`, resolve `AppDbContext`, and assert that `CropTypes`, `CropVarieties`, and `CropReferenceProfiles` are empty.

**Step 2: Run the test to verify it fails**

Run `dotnet test backend/AgriAssist.Api.Tests/AgriAssist.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~StartupDataSeedingTests`.

Expected: failure because startup currently inserts three `CropTypes`.

### Task 2: Remove demo crop startup seeding

**Files:**
- Delete: `backend/AgriAssist.Api/Data/SeedData.cs`
- Modify: `backend/AgriAssist.Api/Program.cs`

**Step 1: Remove the startup call**

Delete only `await SeedData.SeedAsync(dbContext);`. Keep the existing scoped `AppDbContext` resolution and `EnsureCreatedAsync` call for in-memory databases.

**Step 2: Delete the demo-only class**

Delete `SeedData.cs`, whose only behavior is inserting Rice, Maize, and Vegetables.

**Step 3: Run the targeted test**

Run the filtered command from Task 1. Expected: pass with all three collections empty.

### Task 3: Verify and commit

**Step 1:** Restore and build the backend test project in Release mode.

**Step 2:** Run all backend tests without rebuilding.

**Step 3:** Scan for conflict markers, run `git diff --check`, inspect the diff, and show `git status --short --branch`.

**Step 4:** Commit locally with `fix: stop seeding demo crop types on startup`.
