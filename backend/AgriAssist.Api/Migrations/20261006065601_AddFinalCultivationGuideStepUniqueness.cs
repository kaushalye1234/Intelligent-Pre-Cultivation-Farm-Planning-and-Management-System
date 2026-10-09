using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalCultivationGuideStepUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some deployed databases received the same index under migration
            // 20261005185337 before this migration was merged. Reconcile its
            // history without dropping or silently accepting a different index.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AgentSteps_FinalGuideRevision"
                ON "AgentSteps" ("AgentWorkflowId", "CandidateRevision")
                WHERE "AgentName" = 'FinalCultivationGuideAgent' AND "IsDeleted" = FALSE;
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = current_schema()
                          AND indexname = 'IX_AgentSteps_FinalGuideRevision'
                          AND indexdef LIKE 'CREATE UNIQUE INDEX %'
                          AND indexdef LIKE '%("AgentWorkflowId", "CandidateRevision")%'
                          AND indexdef LIKE '%FinalCultivationGuideAgent%'
                          AND indexdef LIKE '%"IsDeleted" = false%'
                    ) THEN
                        RAISE EXCEPTION 'Final guide revision index has an unexpected definition';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM "__EFMigrationsHistory"
                        WHERE "MigrationId" = '20261005185337_AddFinalCultivationGuideStepUniqueness'
                    ) THEN
                        DROP INDEX IF EXISTS "IX_AgentSteps_FinalGuideRevision";
                    END IF;
                END $$;
                """);
        }
    }
}
