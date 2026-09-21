# Database Migration Repair Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Align PostgreSQL with the checked-in EF Core model so farmer registration succeeds.

**Architecture:** Apply existing migrations in order, verify none remain pending, and run backend regression tests. No application code or migration source changes.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, xUnit

---

### Task 1: Apply migrations

**Files:**
- Database: schema and `__EFMigrationsHistory`

1. Confirm the two known migrations are pending.
2. Run `dotnet ef database update --project backend/AgriAssist.Api --startup-project backend/AgriAssist.Api`.
3. Confirm no migration remains pending.

### Task 2: Verify the backend

**Files:**
- Test: `backend/AgriAssist.Api.Tests/`

1. Restore and build the backend test project.
2. Run all backend tests.
3. Run `git diff --check` and confirm the branch remains unchanged.
