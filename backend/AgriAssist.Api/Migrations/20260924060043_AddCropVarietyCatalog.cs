using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCropVarietyCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CropVarieties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CropTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropVarieties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropVarieties_CropTypes_CropTypeId",
                        column: x => x.CropTypeId,
                        principalTable: "CropTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CropVarieties_CropTypeId_Name",
                table: "CropVarieties",
                columns: new[] { "CropTypeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CropVarieties_IsActive",
                table: "CropVarieties",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CropVarieties");
        }
    }
}
