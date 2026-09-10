using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class ApplyFullConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_CropPlanRequests_CropPlanRequestId",
                table: "AgentWorkflows");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_Users_InitiatedByUserId",
                table: "AgentWorkflows");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflows_AgentWorkflowId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_FarmTasks_FarmTaskId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_IrrigationSchedules_IrrigationScheduleId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_Users_DecidedByUserId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_FarmTasks_Farms_FarmId",
                table: "FarmTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_FarmTasks_Users_AssignedToUserId",
                table: "FarmTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldInspections_Fields_FieldId",
                table: "FieldInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldInspections_Users_InspectorUserId",
                table: "FieldInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryStocks_Resources_ResourceId",
                table: "InventoryStocks");

            migrationBuilder.DropForeignKey(
                name: "FK_IrrigationSchedules_Fields_FieldId",
                table: "IrrigationSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceReservations_InventoryStocks_InventoryStockId",
                table: "ResourceReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceReservations_Users_RequestedByUserId",
                table: "ResourceReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Resources_ResourceCategories_ResourceCategoryId",
                table: "Resources");

            migrationBuilder.DropForeignKey(
                name: "FK_Resources_Suppliers_SupplierId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_InventoryStocks_ResourceId",
                table: "InventoryStocks");

            migrationBuilder.DropIndex(
                name: "IX_AgentSteps_AgentWorkflowId",
                table: "AgentSteps");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Suppliers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Suppliers",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Suppliers",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "StockTransactions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "StockTransactions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "StockTransactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Resources",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Resources",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ResourceReservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ResourceReservations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "ResourceReservations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ResourceCategories",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ResourceCategories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IrrigationSchedules",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "IrrigationSchedules",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "ReservedQuantity",
                table: "InventoryStocks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityOnHand",
                table: "InventoryStocks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LowStockThreshold",
                table: "InventoryStocks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "ObservationType",
                table: "InspectionObservations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "InspectionObservations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "InspectionImages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "InspectionImages",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "InspectionImages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Recommendation",
                table: "FollowUpRecommendations",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "FieldInspections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "FieldInspections",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "FarmTasks",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "FarmTasks",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "FarmTasks",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "CropIssues",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "CropIssues",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "CropIssues",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CropIssues",
                type: "character varying(1500)",
                maxLength: 1500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Decision",
                table: "ApprovalDecisions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "ApprovalDecisions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AgentWorkflows",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Objective",
                table: "AgentWorkflows",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CurrentStep",
                table: "AgentWorkflows",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ValidatorName",
                table: "AgentValidationResults",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");


            migrationBuilder.Sql(@"ALTER TABLE ""AgentValidationResults"" ALTER COLUMN ""ErrorsJson"" TYPE jsonb USING ""ErrorsJson""::jsonb;");

            migrationBuilder.AlterColumn<string>(
                name: "ToolName",
                table: "AgentToolExecutions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AgentToolExecutions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");


            migrationBuilder.Sql(@"ALTER TABLE ""AgentToolExecutions"" ALTER COLUMN ""OutputJson"" TYPE jsonb USING ""OutputJson""::jsonb;");


            migrationBuilder.Sql(@"ALTER TABLE ""AgentToolExecutions"" ALTER COLUMN ""InputJson"" TYPE jsonb USING ""InputJson""::jsonb;");

            migrationBuilder.AlterColumn<string>(
                name: "StepName",
                table: "AgentSteps",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AgentSteps",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");


            migrationBuilder.Sql(@"ALTER TABLE ""AgentSteps"" ALTER COLUMN ""OutputJson"" TYPE jsonb USING ""OutputJson""::jsonb;");


            migrationBuilder.Sql(@"ALTER TABLE ""AgentSteps"" ALTER COLUMN ""InputJson"" TYPE jsonb USING ""InputJson""::jsonb;");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessageSafe",
                table: "AgentSteps",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                table: "AgentSteps",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AgentName",
                table: "AgentSteps",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_Type",
                table: "StockTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_IsActive",
                table: "Resources",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Name",
                table: "Resources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceReservations_Status",
                table: "ResourceReservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceCategories_Name",
                table: "ResourceCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationSchedules_ScheduledAt",
                table: "IrrigationSchedules",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_IrrigationSchedules_Status",
                table: "IrrigationSchedules",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStocks_ResourceId",
                table: "InventoryStocks",
                column: "ResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FollowUpRecommendations_IsCompleted",
                table: "FollowUpRecommendations",
                column: "IsCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_FieldInspections_ScheduledAt",
                table: "FieldInspections",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_FieldInspections_Status",
                table: "FieldInspections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_DueAt",
                table: "FarmTasks",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_FarmTasks_Status",
                table: "FarmTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CropIssues_Severity",
                table: "CropIssues",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_CropIssues_Status",
                table: "CropIssues",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_Decision",
                table: "ApprovalDecisions",
                column: "Decision");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflows_Status",
                table: "AgentWorkflows",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSteps_AgentWorkflowId_Sequence",
                table: "AgentSteps",
                columns: new[] { "AgentWorkflowId", "Sequence" });

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_CropPlanRequests_CropPlanRequestId",
                table: "AgentWorkflows",
                column: "CropPlanRequestId",
                principalTable: "CropPlanRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_Users_InitiatedByUserId",
                table: "AgentWorkflows",
                column: "InitiatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflows_AgentWorkflowId",
                table: "ApprovalDecisions",
                column: "AgentWorkflowId",
                principalTable: "AgentWorkflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_FarmTasks_FarmTaskId",
                table: "ApprovalDecisions",
                column: "FarmTaskId",
                principalTable: "FarmTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_IrrigationSchedules_IrrigationScheduleId",
                table: "ApprovalDecisions",
                column: "IrrigationScheduleId",
                principalTable: "IrrigationSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_Users_DecidedByUserId",
                table: "ApprovalDecisions",
                column: "DecidedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FarmTasks_Farms_FarmId",
                table: "FarmTasks",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FarmTasks_Users_AssignedToUserId",
                table: "FarmTasks",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldInspections_Fields_FieldId",
                table: "FieldInspections",
                column: "FieldId",
                principalTable: "Fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldInspections_Users_InspectorUserId",
                table: "FieldInspections",
                column: "InspectorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStocks_Resources_ResourceId",
                table: "InventoryStocks",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IrrigationSchedules_Fields_FieldId",
                table: "IrrigationSchedules",
                column: "FieldId",
                principalTable: "Fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceReservations_InventoryStocks_InventoryStockId",
                table: "ResourceReservations",
                column: "InventoryStockId",
                principalTable: "InventoryStocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceReservations_Users_RequestedByUserId",
                table: "ResourceReservations",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_ResourceCategories_ResourceCategoryId",
                table: "Resources",
                column: "ResourceCategoryId",
                principalTable: "ResourceCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_Suppliers_SupplierId",
                table: "Resources",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_CropPlanRequests_CropPlanRequestId",
                table: "AgentWorkflows");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentWorkflows_Users_InitiatedByUserId",
                table: "AgentWorkflows");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflows_AgentWorkflowId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_FarmTasks_FarmTaskId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_IrrigationSchedules_IrrigationScheduleId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_Users_DecidedByUserId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_FarmTasks_Farms_FarmId",
                table: "FarmTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_FarmTasks_Users_AssignedToUserId",
                table: "FarmTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldInspections_Fields_FieldId",
                table: "FieldInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldInspections_Users_InspectorUserId",
                table: "FieldInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryStocks_Resources_ResourceId",
                table: "InventoryStocks");

            migrationBuilder.DropForeignKey(
                name: "FK_IrrigationSchedules_Fields_FieldId",
                table: "IrrigationSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceReservations_InventoryStocks_InventoryStockId",
                table: "ResourceReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceReservations_Users_RequestedByUserId",
                table: "ResourceReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Resources_ResourceCategories_ResourceCategoryId",
                table: "Resources");

            migrationBuilder.DropForeignKey(
                name: "FK_Resources_Suppliers_SupplierId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_Type",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Resources_IsActive",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Resources_Name",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_ResourceReservations_Status",
                table: "ResourceReservations");

            migrationBuilder.DropIndex(
                name: "IX_ResourceCategories_Name",
                table: "ResourceCategories");

            migrationBuilder.DropIndex(
                name: "IX_IrrigationSchedules_ScheduledAt",
                table: "IrrigationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_IrrigationSchedules_Status",
                table: "IrrigationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_InventoryStocks_ResourceId",
                table: "InventoryStocks");

            migrationBuilder.DropIndex(
                name: "IX_FollowUpRecommendations_IsCompleted",
                table: "FollowUpRecommendations");

            migrationBuilder.DropIndex(
                name: "IX_FieldInspections_ScheduledAt",
                table: "FieldInspections");

            migrationBuilder.DropIndex(
                name: "IX_FieldInspections_Status",
                table: "FieldInspections");

            migrationBuilder.DropIndex(
                name: "IX_FarmTasks_DueAt",
                table: "FarmTasks");

            migrationBuilder.DropIndex(
                name: "IX_FarmTasks_Status",
                table: "FarmTasks");

            migrationBuilder.DropIndex(
                name: "IX_CropIssues_Severity",
                table: "CropIssues");

            migrationBuilder.DropIndex(
                name: "IX_CropIssues_Status",
                table: "CropIssues");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_Decision",
                table: "ApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflows_Status",
                table: "AgentWorkflows");

            migrationBuilder.DropIndex(
                name: "IX_AgentSteps_AgentWorkflowId_Sequence",
                table: "AgentSteps");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Suppliers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Suppliers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "ContactEmail",
                table: "Suppliers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(180)",
                oldMaxLength: 180);

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "StockTransactions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "StockTransactions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "StockTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Resources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Resources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ResourceReservations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ResourceReservations",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Purpose",
                table: "ResourceReservations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ResourceCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "ResourceCategories",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "IrrigationSchedules",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "IrrigationSchedules",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReservedQuantity",
                table: "InventoryStocks",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityOnHand",
                table: "InventoryStocks",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "LowStockThreshold",
                table: "InventoryStocks",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "ObservationType",
                table: "InspectionObservations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "InspectionObservations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "InspectionImages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "InspectionImages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(240)",
                oldMaxLength: 240);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "InspectionImages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<string>(
                name: "Recommendation",
                table: "FollowUpRecommendations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1500)",
                oldMaxLength: 1500);

            migrationBuilder.AlterColumn<string>(
                name: "Summary",
                table: "FieldInspections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "FieldInspections",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "FarmTasks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "FarmTasks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "FarmTasks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1500)",
                oldMaxLength: 1500);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "CropIssues",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "CropIssues",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<int>(
                name: "Severity",
                table: "CropIssues",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "CropIssues",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1500)",
                oldMaxLength: 1500);

            migrationBuilder.AlterColumn<int>(
                name: "Decision",
                table: "ApprovalDecisions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "ApprovalDecisions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "AgentWorkflows",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Objective",
                table: "AgentWorkflows",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "CurrentStep",
                table: "AgentWorkflows",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "ValidatorName",
                table: "AgentValidationResults",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorsJson",
                table: "AgentValidationResults",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "ToolName",
                table: "AgentToolExecutions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "AgentToolExecutions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "OutputJson",
                table: "AgentToolExecutions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "InputJson",
                table: "AgentToolExecutions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "StepName",
                table: "AgentSteps",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "AgentSteps",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "OutputJson",
                table: "AgentSteps",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "InputJson",
                table: "AgentSteps",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessageSafe",
                table: "AgentSteps",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorCode",
                table: "AgentSteps",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AgentName",
                table: "AgentSteps",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStocks_ResourceId",
                table: "InventoryStocks",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSteps_AgentWorkflowId",
                table: "AgentSteps",
                column: "AgentWorkflowId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_CropPlanRequests_CropPlanRequestId",
                table: "AgentWorkflows",
                column: "CropPlanRequestId",
                principalTable: "CropPlanRequests",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentWorkflows_Users_InitiatedByUserId",
                table: "AgentWorkflows",
                column: "InitiatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflows_AgentWorkflowId",
                table: "ApprovalDecisions",
                column: "AgentWorkflowId",
                principalTable: "AgentWorkflows",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_FarmTasks_FarmTaskId",
                table: "ApprovalDecisions",
                column: "FarmTaskId",
                principalTable: "FarmTasks",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_IrrigationSchedules_IrrigationScheduleId",
                table: "ApprovalDecisions",
                column: "IrrigationScheduleId",
                principalTable: "IrrigationSchedules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_Users_DecidedByUserId",
                table: "ApprovalDecisions",
                column: "DecidedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FarmTasks_Farms_FarmId",
                table: "FarmTasks",
                column: "FarmId",
                principalTable: "Farms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FarmTasks_Users_AssignedToUserId",
                table: "FarmTasks",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldInspections_Fields_FieldId",
                table: "FieldInspections",
                column: "FieldId",
                principalTable: "Fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldInspections_Users_InspectorUserId",
                table: "FieldInspections",
                column: "InspectorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStocks_Resources_ResourceId",
                table: "InventoryStocks",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IrrigationSchedules_Fields_FieldId",
                table: "IrrigationSchedules",
                column: "FieldId",
                principalTable: "Fields",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceReservations_InventoryStocks_InventoryStockId",
                table: "ResourceReservations",
                column: "InventoryStockId",
                principalTable: "InventoryStocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceReservations_Users_RequestedByUserId",
                table: "ResourceReservations",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_ResourceCategories_ResourceCategoryId",
                table: "Resources",
                column: "ResourceCategoryId",
                principalTable: "ResourceCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_Suppliers_SupplierId",
                table: "Resources",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");
        }
    }
}



