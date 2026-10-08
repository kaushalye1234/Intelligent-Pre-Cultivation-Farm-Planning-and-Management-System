using AgriAssist.Api.Data;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Models.Shared;
using AgriAssist.Api.Dtos.CropPlanning;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace AgriAssist.Api.Tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class CropReferenceVerificationPostgreSqlTests
{
    [PostgreSqlFact]
    public async Task Legacy_upgrade_preserves_evidence_and_rollback_refuses_unverified_drafts()
    {
        var connection = Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")!;
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Host is not ("localhost" or "127.0.0.1") || builder.Port is not (5432 or 55432)
            || (!(builder.Database ?? "").StartsWith("agriassist_member4_demo_", StringComparison.Ordinal)
                && builder.Database != "agriassist_tests"))
            throw new InvalidOperationException("Use a disposable local Member 4 database or the isolated CI PostgreSQL service.");
        var database = "agriassist_member4_demo_migration_" + Guid.NewGuid().ToString("N");
        builder.Database = "postgres";
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE DATABASE " + database, admin)) await create.ExecuteNonQueryAsync();
        try
        {
            builder.Database = database;
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var crop = new CropType { Name = "Synthetic rice" };
            var legacy = new CropReferenceProfile { CropType = crop, SourceName = "SYNTHETIC LEGACY SOURCE",
                SourceUrl = "https://example.test/source", SourceVersion = "legacy", VerifiedAt = DateTime.UtcNow.AddDays(-2),
                Stages = [new CropStageReference { StageName = "Maturity", Sequence = 1, TypicalMinDays = 98, TypicalMaxDays = 102,
                    SourceName = "SYNTHETIC LEGACY SOURCE" }] };
            db.Add(legacy);
            await db.SaveChangesAsync();
            var originalTime = await db.CropReferenceProfiles.AsNoTracking().Where(item => item.Id == legacy.Id).Select(item => item.VerifiedAt).SingleAsync();
            var baseline = db.Database.GetMigrations().TakeWhile(item => item != "20261007113120_AddCropReferenceVerification").Last();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(baseline);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var upgraded = await db.CropReferenceProfiles.AsNoTracking().Include(item => item.Stages).SingleAsync(item => item.Id == legacy.Id);
            Assert.Equal(CropReferenceVerificationState.LegacyReviewRequired, upgraded.VerificationState);
            Assert.Null(upgraded.VerifiedByUserId);
            Assert.Equal(originalTime, upgraded.VerifiedAt);
            Assert.True(upgraded.IsActive);
            Assert.Equal("legacy", upgraded.SourceVersion);
            Assert.Single(upgraded.Stages);
            var draft = new CropReferenceProfile { CropTypeId = crop.Id, SourceName = "SYNTHETIC DRAFT",
                SourceVersion = "draft", IsActive = false, VerificationState = CropReferenceVerificationState.Draft };
            db.Add(draft);
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(baseline));
            Assert.Contains("Cannot roll back verification", error.MessageText);
            db.ChangeTracker.Clear();
            Assert.Null((await db.CropReferenceProfiles.AsNoTracking().SingleAsync(item => item.Id == draft.Id)).VerifiedAt);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand("DROP DATABASE " + database + " WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [PostgreSqlFact]
    public async Task Winning_draft_edit_rolls_back_concurrent_officer_verification_and_field_record()
    {
        var connection = Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")!;
        Guid fieldId;
        Guid referenceId;
        Guid officerId;
        await using (var seedDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
        {
            await seedDb.Database.MigrateAsync();
            var seed = await WeatherResourceTestData.SeedAsync(seedDb, reserved: 0, cropTypeName: "SYNTHETIC VERIFICATION " + Guid.NewGuid().ToString("N"));
            var reference = await CropReferenceVerificationTests.PrepareDraft(seedDb, seed.CropTypeId, seed.ResourceId);
            var officer = new AppUser { FullName = "Synthetic Officer", Email = "verification-" + Guid.NewGuid().ToString("N") + "@example.test",
                PasswordHash = "hash", Role = ApplicationRole.AgriculturalOfficer };
            seedDb.Add(officer);
            await seedDb.SaveChangesAsync();
            fieldId = seed.FieldId; referenceId = reference.Id; officerId = officer.Id;
        }
        var gate = new SaveGate();
        await using var verifyDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).AddInterceptors(gate).Options);
        var verification = CropReferenceVerificationTests.CreateService(verifyDb, officerId).VerifyAsync(referenceId,
            new VerifyReferenceRequest(fieldId, WaterRegime.Irrigated, "Synthetic concurrent officer review", 1, true), CancellationToken.None);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var editDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
        {
            var reference = await editDb.CropReferenceProfiles.SingleAsync(item => item.Id == referenceId);
            reference.SourceVersion = "Changed while review was running";
            reference.DraftVersion++;
            await editDb.SaveChangesAsync();
        }
        gate.Release.SetResult();
        var error = await Assert.ThrowsAsync<ApiException>(() => verification);
        Assert.Equal("REFERENCE_DRAFT_STALE", error.Code);
        await using var check = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        var remaining = await check.CropReferenceProfiles.AsNoTracking().SingleAsync(item => item.Id == referenceId);
        Assert.Equal(CropReferenceVerificationState.Draft, remaining.VerificationState);
        Assert.Equal(2, remaining.DraftVersion);
        Assert.Null(remaining.VerifiedAt);
        Assert.False(remaining.IsActive);
        Assert.False(await check.FieldWaterRegimeVerifications.AnyAsync(item => item.FieldId == fieldId));
    }

    private sealed class SaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGRIASSIST_TEST_POSTGRES_CONNECTION_STRING")))
                Skip = "Disposable PostgreSQL connection is required.";
        }
    }
}
