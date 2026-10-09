using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCropPlanCancellationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "CropPlanRequestHistories",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "StatusChanged");

            migrationBuilder.AddColumn<string>(
                name: "ChangedByRole",
                table: "CropPlanRequestHistories",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CropPlanRequestHistories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "CropPlanRequestHistories");

            migrationBuilder.DropColumn(
                name: "ChangedByRole",
                table: "CropPlanRequestHistories");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CropPlanRequestHistories");
        }
    }
}
