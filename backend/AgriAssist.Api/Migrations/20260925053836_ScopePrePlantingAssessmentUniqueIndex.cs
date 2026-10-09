using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScopePrePlantingAssessmentUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FieldInspections_CropPlanRequestId_Purpose",
                table: "FieldInspections");

            migrationBuilder.CreateIndex(
                name: "IX_FieldInspections_CropPlanRequestId",
                table: "FieldInspections",
                column: "CropPlanRequestId",
                unique: true,
                filter: "\"CropPlanRequestId\" IS NOT NULL AND \"Purpose\" = 'PrePlanting'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FieldInspections_CropPlanRequestId",
                table: "FieldInspections");

            migrationBuilder.CreateIndex(
                name: "IX_FieldInspections_CropPlanRequestId_Purpose",
                table: "FieldInspections",
                columns: new[] { "CropPlanRequestId", "Purpose" },
                unique: true);
        }
    }
}
