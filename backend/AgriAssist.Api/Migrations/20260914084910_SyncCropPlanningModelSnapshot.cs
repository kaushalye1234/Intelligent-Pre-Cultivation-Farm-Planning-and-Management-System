using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <summary>
    /// Schema no-op. The crop reference tables are created by the hand-written
    /// 20260913000100_AddCropPlanningAi migration, but they were never recorded in the
    /// model snapshot. This migration only brings the snapshot up to date so future
    /// migrations do not try to create those tables a second time.
    /// </summary>
    public partial class SyncCropPlanningModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
