using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmerCropPlanContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CropVarietyId",
                table: "CropPlanRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CultivationSeason",
                table: "CropPlanRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotSure");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousCropTypeId",
                table: "CropPlanRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousKnownProblemsJson",
                table: "CropPlanRequests",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_CropPlanRequests_CropVarietyId",
                table: "CropPlanRequests",
                column: "CropVarietyId");

            migrationBuilder.CreateIndex(
                name: "IX_CropPlanRequests_PreviousCropTypeId",
                table: "CropPlanRequests",
                column: "PreviousCropTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CropPlanRequests_CropTypes_PreviousCropTypeId",
                table: "CropPlanRequests",
                column: "PreviousCropTypeId",
                principalTable: "CropTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CropPlanRequests_CropVarieties_CropVarietyId",
                table: "CropPlanRequests",
                column: "CropVarietyId",
                principalTable: "CropVarieties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CropPlanRequests_CropTypes_PreviousCropTypeId",
                table: "CropPlanRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CropPlanRequests_CropVarieties_CropVarietyId",
                table: "CropPlanRequests");

            migrationBuilder.DropIndex(
                name: "IX_CropPlanRequests_CropVarietyId",
                table: "CropPlanRequests");

            migrationBuilder.DropIndex(
                name: "IX_CropPlanRequests_PreviousCropTypeId",
                table: "CropPlanRequests");

            migrationBuilder.DropColumn(
                name: "CropVarietyId",
                table: "CropPlanRequests");

            migrationBuilder.DropColumn(
                name: "CultivationSeason",
                table: "CropPlanRequests");

            migrationBuilder.DropColumn(
                name: "PreviousCropTypeId",
                table: "CropPlanRequests");

            migrationBuilder.DropColumn(
                name: "PreviousKnownProblemsJson",
                table: "CropPlanRequests");
        }
    }
}
