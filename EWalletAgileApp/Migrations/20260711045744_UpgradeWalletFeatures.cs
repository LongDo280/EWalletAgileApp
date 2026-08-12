using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EWalletAgileApp.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeWalletFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyLimit",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumBalance",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionLimit",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                columns: new[] { "DailyLimit", "MaximumBalance", "TransactionLimit" },
                values: new object[] { 50000000m, 100000000m, 20000000m });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 2,
                columns: new[] { "DailyLimit", "MaximumBalance", "TransactionLimit" },
                values: new object[] { 50000000m, 100000000m, 20000000m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyLimit",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MaximumBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TransactionLimit",
                table: "Users");
        }
    }
}
