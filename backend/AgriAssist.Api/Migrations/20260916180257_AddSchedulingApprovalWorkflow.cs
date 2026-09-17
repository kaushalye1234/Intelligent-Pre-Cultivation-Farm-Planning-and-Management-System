using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentValidationResults_AgentWorkflowId",
                table: "AgentValidationResults");

            migrationBuilder.AddColumn<int>(
                name: "CandidateRevision",
                table: "ResourceReservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedByWorkflowId",
                table: "ResourceReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CandidateRevision",
                table: "IrrigationSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedByWorkflowId",
                table: "IrrigationSchedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CandidateRevision",
                table: "FarmTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedByWorkflowId",
                table: "FarmTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CandidateRevision",
                table: "ApprovalDecisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedWorkflowVersion",
                table: "ApprovalDecisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ApprovalDecisions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CandidateRevision",
                table: "AgentWorkflows",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RevisionCount",
                table: "AgentWorkflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "AgentWorkflows",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CandidateRevision",
                table: "AgentValidationResults",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "WarningsJson",
                table: "AgentValidationResults",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "CandidateRevision",
                table: "AgentSteps",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceReservations_GeneratedByWorkflowId_CandidateRevision",
                table: "ResourceReservations",
                columns: new[] { "GeneratedByWorkflowId", "CandidateRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationSchedules_GeneratedByWorkflowId_CandidateRevision",
                table: "IrrigationSchedules",
                columns: new[] { "GeneratedByWorkflowId", "CandidateRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_GeneratedByWorkflowId_CandidateRevision",
                table: "FarmTasks",
                columns: new[] { "GeneratedByWorkflowId", "CandidateRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_AgentWorkflowId_IdempotencyKey",
                table: "ApprovalDecisions",
                columns: new[] { "AgentWorkflowId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentValidationResults_AgentWorkflowId_CandidateRevision",
                table: "AgentValidationResults",
                columns: new[] { "AgentWorkflowId", "CandidateRevision" });

            migrationBuilder.AddForeignKey(
                name: "FK_FarmTasks_AgentWorkflows_GeneratedByWorkflowId",
                table: "FarmTasks",
                column: "GeneratedByWorkflowId",
                principalTable: "AgentWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IrrigationSchedules_AgentWorkflows_GeneratedByWorkflowId",
                table: "IrrigationSchedules",
                column: "GeneratedByWorkflowId",
                principalTable: "AgentWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceReservations_AgentWorkflows_GeneratedByWorkflowId",
                table: "ResourceReservations",
                column: "GeneratedByWorkflowId",
                principalTable: "AgentWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FarmTasks_AgentWorkflows_GeneratedByWorkflowId",
                table: "FarmTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_IrrigationSchedules_AgentWorkflows_GeneratedByWorkflowId",
                table: "IrrigationSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceReservations_AgentWorkflows_GeneratedByWorkflowId",
                table: "ResourceReservations");

            migrationBuilder.DropIndex(
                name: "IX_ResourceReservations_GeneratedByWorkflowId_CandidateRevision",
                table: "ResourceReservations");

            migrationBuilder.DropIndex(
                name: "IX_IrrigationSchedules_GeneratedByWorkflowId_CandidateRevision",
                table: "IrrigationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_FarmTasks_GeneratedByWorkflowId_CandidateRevision",
                table: "FarmTasks");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_AgentWorkflowId_IdempotencyKey",
                table: "ApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "IX_AgentValidationResults_AgentWorkflowId_CandidateRevision",
                table: "AgentValidationResults");

            migrationBuilder.DropColumn(
                name: "CandidateRevision",
                table: "ResourceReservations");

            migrationBuilder.DropColumn(
                name: "GeneratedByWorkflowId",
                table: "ResourceReservations");

            migrationBuilder.DropColumn(
                name: "CandidateRevision",
                table: "IrrigationSchedules");

            migrationBuilder.DropColumn(
                name: "GeneratedByWorkflowId",
                table: "IrrigationSchedules");

            migrationBuilder.DropColumn(
                name: "CandidateRevision",
                table: "FarmTasks");

            migrationBuilder.DropColumn(
                name: "GeneratedByWorkflowId",
                table: "FarmTasks");

            migrationBuilder.DropColumn(
                name: "CandidateRevision",
                table: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "ExpectedWorkflowVersion",
                table: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "CandidateRevision",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "RevisionCount",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "AgentWorkflows");

            migrationBuilder.DropColumn(
                name: "CandidateRevision",
                table: "AgentValidationResults");

            migrationBuilder.DropColumn(
                name: "WarningsJson",
                table: "AgentValidationResults");

            migrationBuilder.DropColumn(
                name: "CandidateRevision",
                table: "AgentSteps");

            migrationBuilder.CreateIndex(
                name: "IX_AgentValidationResults_AgentWorkflowId",
                table: "AgentValidationResults",
                column: "AgentWorkflowId");
        }
    }
}
