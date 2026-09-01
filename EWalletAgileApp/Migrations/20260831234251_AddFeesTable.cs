using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EWalletAgileApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFeesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Fees",
                columns: table => new
                {
                    FeeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fees", x => x.FeeId);
                });

            migrationBuilder.InsertData(
                table: "Fees",
                columns: new[] { "FeeId", "FeeType", "IsActive", "MaxFee", "TransactionType", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { 1, "Percent", true, 0m, "Deposit", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 0m },
                    { 2, "Percent", true, 20000m, "Withdraw", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fees");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                table: "Transactions");
        }
    }
}
