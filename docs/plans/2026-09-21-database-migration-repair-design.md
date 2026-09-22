# Database Migration Repair Design

## Problem

Farmer registration returns HTTP 500 because the configured PostgreSQL database is behind the checked-in EF Core model. `AddSchedulingApprovalWorkflow` and `AddUserSecurityState` are pending; registration depends on columns introduced by the user-security migration.

## Options considered

1. Apply the checked-in EF Core migrations. This preserves data and aligns the schema with the application model.
2. Use the development in-memory database. This avoids migrations but loses persistence and does not verify PostgreSQL behavior.
3. Alter the schema manually. This risks divergence from EF Core migration history.

## Approved design

Apply all pending migrations in their defined order with `dotnet ef database update`. Do not edit migrations or application code. Verify that EF reports no pending migrations, then run the backend test suite.

## Safety and rollback

The target is the database configured by `backend/AgriAssist.Api/.env`. The command changes database schema and migration history but does not delete the database. If deployment fails, stop and report the failing migration. Any rollback should use the migration `Down` methods and an appropriate database backup.
