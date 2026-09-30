using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class Member4ResourceQuantityPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "StockTransactions",
                type: "numeric(13,3)",
                precision: 13,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ResourceReservations",
                type: "numeric(13,3)",
                precision: 13,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReservedQuantity",
                table: "InventoryStocks",
                type: "numeric(13,3)",
                precision: 13,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityOnHand",
                table: "InventoryStocks",
                type: "numeric(13,3)",
                precision: 13,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "LowStockThreshold",
                table: "InventoryStocks",
                type: "numeric(13,3)",
                precision: 13,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "StockTransactions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(13,3)",
                oldPrecision: 13,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ResourceReservations",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(13,3)",
                oldPrecision: 13,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReservedQuantity",
                table: "InventoryStocks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(13,3)",
                oldPrecision: 13,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "QuantityOnHand",
                table: "InventoryStocks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(13,3)",
                oldPrecision: 13,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "LowStockThreshold",
                table: "InventoryStocks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(13,3)",
                oldPrecision: 13,
                oldScale: 3);
        }
    }
}
