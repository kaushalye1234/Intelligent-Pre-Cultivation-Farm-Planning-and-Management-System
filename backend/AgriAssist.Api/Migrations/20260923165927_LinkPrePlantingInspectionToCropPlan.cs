using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkPrePlantingInspectionToCropPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CropPlanRequestId",
                table: "FieldInspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "FieldInspections",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Routine");

            migrationBuilder.CreateIndex(
                name: "IX_FieldInspections_CropPlanRequestId_Purpose",
                table: "FieldInspections",
                columns: new[] { "CropPlanRequestId", "Purpose" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldInspections_CropPlanRequests_CropPlanRequestId",
                table: "FieldInspections",
                column: "CropPlanRequestId",
                principalTable: "CropPlanRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldInspections_CropPlanRequests_CropPlanRequestId",
                table: "FieldInspections");

            migrationBuilder.DropIndex(
                name: "IX_FieldInspections_CropPlanRequestId_Purpose",
                table: "FieldInspections");

            migrationBuilder.DropColumn(
                name: "CropPlanRequestId",
                table: "FieldInspections");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "FieldInspections");
        }
    }
}
