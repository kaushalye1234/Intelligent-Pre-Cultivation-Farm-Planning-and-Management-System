using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionImageAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionImages_FieldInspectionId",
                table: "InspectionImages");

            migrationBuilder.AddColumn<string>(
                name: "AssetId",
                table: "InspectionImages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentSha256",
                table: "InspectionImages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryType",
                table: "InspectionImages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "upload");

            migrationBuilder.AddColumn<bool>(
                name: "IsRepresentativeForAi",
                table: "InspectionImages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "StorageVersion",
                table: "InspectionImages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FrozenImageAnalysisReviewId",
                table: "FieldInspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InspectionImageAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CropTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CropVarietyId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourcePolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourcePolicyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    PromptContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ImagePreprocessingVersion = table.Column<int>(type: "integer", nullable: false),
                    RelevanceRuleVersion = table.Column<int>(type: "integer", nullable: false),
                    Pass1ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    EvidencePacketJson = table.Column<string>(type: "jsonb", nullable: true),
                    FinalResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FailureMessageSafe = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionImageAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionImageAnalyses_FieldInspections_FieldInspectionId",
                        column: x => x.FieldInspectionId,
                        principalTable: "FieldInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionImageAnalyses_InspectionImages_InspectionImageId",
                        column: x => x.InspectionImageId,
                        principalTable: "InspectionImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionImageAnalysisReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionImageAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Disposition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReviewedProjectionJson = table.Column<string>(type: "jsonb", nullable: true),
                    EditedFieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    StaffNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionImageAnalysisReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionImageAnalysisReviews_InspectionImageAnalyses_Insp~",
                        column: x => x.InspectionImageAnalysisId,
                        principalTable: "InspectionImageAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspectionImageAnalysisReviews_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_FieldInspectionId",
                table: "InspectionImages",
                column: "FieldInspectionId",
                unique: true,
                filter: "\"IsRepresentativeForAi\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_FieldInspections_FrozenImageAnalysisReviewId",
                table: "FieldInspections",
                column: "FrozenImageAnalysisReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImageAnalyses_FieldInspectionId",
                table: "InspectionImageAnalyses",
                column: "FieldInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImageAnalyses_InspectionImageId",
                table: "InspectionImageAnalyses",
                column: "InspectionImageId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImageAnalyses_InspectionImageId_AnalysisFingerpri~",
                table: "InspectionImageAnalyses",
                columns: new[] { "InspectionImageId", "AnalysisFingerprint" },
                unique: true,
                filter: "\"Status\" IN ('Running', 'Succeeded')");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImageAnalysisReviews_InspectionImageAnalysisId_Re~",
                table: "InspectionImageAnalysisReviews",
                columns: new[] { "InspectionImageAnalysisId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImageAnalysisReviews_ReviewedByUserId",
                table: "InspectionImageAnalysisReviews",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldInspections_InspectionImageAnalysisReviews_FrozenImage~",
                table: "FieldInspections",
                column: "FrozenImageAnalysisReviewId",
                principalTable: "InspectionImageAnalysisReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldInspections_InspectionImageAnalysisReviews_FrozenImage~",
                table: "FieldInspections");

            migrationBuilder.DropTable(
                name: "InspectionImageAnalysisReviews");

            migrationBuilder.DropTable(
                name: "InspectionImageAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_InspectionImages_FieldInspectionId",
                table: "InspectionImages");

            migrationBuilder.DropIndex(
                name: "IX_FieldInspections_FrozenImageAnalysisReviewId",
                table: "FieldInspections");

            migrationBuilder.DropColumn(
                name: "AssetId",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "ContentSha256",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "DeliveryType",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "IsRepresentativeForAi",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "StorageVersion",
                table: "InspectionImages");

            migrationBuilder.DropColumn(
                name: "FrozenImageAnalysisReviewId",
                table: "FieldInspections");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionImages_FieldInspectionId",
                table: "InspectionImages",
                column: "FieldInspectionId");
        }
    }
}
