using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCropReferenceVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "VerifiedAt",
                table: "CropReferenceProfiles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "DraftVersion",
                table: "CropReferenceProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "FieldWaterRegimeVerificationId",
                table: "CropReferenceProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationState",
                table: "CropReferenceProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "LegacyReviewRequired");

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                table: "CropReferenceProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaterRegime",
                table: "CropReferenceProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FieldWaterRegimeVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaterRegime = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Observation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    VerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldWaterRegimeVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldWaterRegimeVerifications_Fields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "Fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FieldWaterRegimeVerifications_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropReferenceProfiles_FieldWaterRegimeVerificationId",
                table: "CropReferenceProfiles",
                column: "FieldWaterRegimeVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CropReferenceProfiles_VerifiedByUserId",
                table: "CropReferenceProfiles",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldWaterRegimeVerifications_FieldId_VerifiedAt",
                table: "FieldWaterRegimeVerifications",
                columns: new[] { "FieldId", "VerifiedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldWaterRegimeVerifications_VerifiedByUserId",
                table: "FieldWaterRegimeVerifications",
                column: "VerifiedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CropReferenceProfiles_FieldWaterRegimeVerifications_FieldWa~",
                table: "CropReferenceProfiles",
                column: "FieldWaterRegimeVerificationId",
                principalTable: "FieldWaterRegimeVerifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CropReferenceProfiles_Users_VerifiedByUserId",
                table: "CropReferenceProfiles",
                column: "VerifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "CropReferenceProfiles" WHERE "VerifiedAt" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back verification while unverified drafts exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CropReferenceProfiles_FieldWaterRegimeVerifications_FieldWa~",
                table: "CropReferenceProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_CropReferenceProfiles_Users_VerifiedByUserId",
                table: "CropReferenceProfiles");

            migrationBuilder.DropTable(
                name: "FieldWaterRegimeVerifications");

            migrationBuilder.DropIndex(
                name: "IX_CropReferenceProfiles_FieldWaterRegimeVerificationId",
                table: "CropReferenceProfiles");

            migrationBuilder.DropIndex(
                name: "IX_CropReferenceProfiles_VerifiedByUserId",
                table: "CropReferenceProfiles");

            migrationBuilder.DropColumn(
                name: "DraftVersion",
                table: "CropReferenceProfiles");

            migrationBuilder.DropColumn(
                name: "FieldWaterRegimeVerificationId",
                table: "CropReferenceProfiles");

            migrationBuilder.DropColumn(
                name: "VerificationState",
                table: "CropReferenceProfiles");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "CropReferenceProfiles");

            migrationBuilder.DropColumn(
                name: "WaterRegime",
                table: "CropReferenceProfiles");

            migrationBuilder.AlterColumn<DateTime>(
                name: "VerifiedAt",
                table: "CropReferenceProfiles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
