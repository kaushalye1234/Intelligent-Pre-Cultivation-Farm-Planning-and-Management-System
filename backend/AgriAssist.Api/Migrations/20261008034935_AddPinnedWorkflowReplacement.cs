using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedWorkflowReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacementIdempotencyKey",
                table: "AgentWorkflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequiredCropReferenceProfileId",
                table: "AgentWorkflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredCropReferenceVersion",
                table: "AgentWorkflows",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesWorkflowId",
                table: "AgentWorkflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_CropPlanRequestId_ReplacementIdempotencyKey",
                table: "AgentWorkflows",
                columns: new[] { "CropPlanRequestId", "ReplacementIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_RequiredCropReferenceProfileId",
                table: "AgentWorkflows",
                column: "RequiredCropReferenceProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_SupersedesWorkflowId",
                table: "AgentWorkflows",
                column: "SupersedesWorkflowId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_AgentWorkflows_SupersedesWorkflowId",
                table: "AgentWorkflows",
                column: "SupersedesWorkflowId",
                principalTable: "AgentWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_CropReferenceProfiles_RequiredCropReferenceP~",
                table: "AgentWorkflows",
                column: "RequiredCropReferenceProfileId",
                principalTable: "CropReferenceProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AgentWorkflows" WHERE "RequiredCropReferenceProfileId" IS NOT NULL OR "SupersedesWorkflowId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back pinned workflows while recovery history exists.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_AgentWorkflows_SupersedesWorkflowId",
                table: "AgentWorkflows");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_CropReferenceProfiles_RequiredCropReferenceP~",
                table: "AgentWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_CropPlanRequestId_ReplacementIdempotencyKey",
                table: "AgentWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_RequiredCropReferenceProfileId",
                table: "AgentWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_SupersedesWorkflowId",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "ReplacementIdempotencyKey",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "RequiredCropReferenceProfileId",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "RequiredCropReferenceVersion",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "SupersedesWorkflowId",
                table: "AgentWorkflows");
        }
    }
}
