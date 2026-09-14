using System;
<<<<<<< HEAD
using AgriAssist.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
=======
>>>>>>> 6f5561abf0c8257dafd53abd629d72ab2b783e9a
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
<<<<<<< HEAD
    // Without these attributes EF Core never discovers this hand-written migration,
    // so the crop reference tables were never created in the database.
    [DbContext(typeof(AppDbContext))]
    [Migration("20260913000100_AddCropPlanningAi")]
=======
>>>>>>> 6f5561abf0c8257dafd53abd629d72ab2b783e9a
    public partial class AddCropPlanningAi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CropReferenceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CropTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VarietyName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Region = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropReferenceProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropReferenceProfiles_CropTypes_CropTypeId",
                        column: x => x.CropTypeId,
                        principalTable: "CropTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CropRuleReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CropReferenceProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RuleKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StructuredValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropRuleReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropRuleReferences_CropReferenceProfiles_CropReferenceProfileId",
                        column: x => x.CropReferenceProfileId,
                        principalTable: "CropReferenceProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CropStageReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CropReferenceProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    TypicalMinDays = table.Column<int>(type: "integer", nullable: true),
                    TypicalMaxDays = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropStageReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropStageReferences_CropReferenceProfiles_CropReferenceProfileId",
                        column: x => x.CropReferenceProfileId,
                        principalTable: "CropReferenceProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_CropReferenceProfiles_CropTypeId_VarietyName_Region_IsActive", table: "CropReferenceProfiles", columns: new[] { "CropTypeId", "VarietyName", "Region", "IsActive" });
            migrationBuilder.CreateIndex(name: "IX_CropReferenceProfiles_VerifiedAt", table: "CropReferenceProfiles", column: "VerifiedAt");
            migrationBuilder.CreateIndex(name: "IX_CropRuleReferences_CropReferenceProfileId_RuleType_RuleKey", table: "CropRuleReferences", columns: new[] { "CropReferenceProfileId", "RuleType", "RuleKey" });
            migrationBuilder.CreateIndex(name: "IX_CropRuleReferences_VerifiedAt", table: "CropRuleReferences", column: "VerifiedAt");
            migrationBuilder.CreateIndex(name: "IX_CropStageReferences_CropReferenceProfileId_Sequence", table: "CropStageReferences", columns: new[] { "CropReferenceProfileId", "Sequence" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CropRuleReferences");
            migrationBuilder.DropTable(name: "CropStageReferences");
            migrationBuilder.DropTable(name: "CropReferenceProfiles");
        }
    }
}
