using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EWalletAgileApp.Migrations
{
    /// <inheritdoc />
    public partial class AddBillPaymentFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidAt",
                table: "Bills",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "BillingPeriod",
                table: "Bills",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Bills",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TransactionId",
                table: "Bills",
                type: "int",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Bills",
                columns: new[] { "BillId", "Amount", "BillType", "BillingPeriod", "CustomerCode", "CustomerName", "PaidAt", "Status", "TransactionId", "UserId" },
                values: new object[,]
                {
                    { 1, 350000m, "Điện", "08/2026", "EVN123456", "Nguyễn Văn A", null, "Unpaid", null, 1 },
                    { 2, 250000m, "Internet", "08/2026", "NET123456", "Nguyễn Văn A", null, "Unpaid", null, 1 },
                    { 3, 180000m, "Nước", "08/2026", "WATER123456", "Trần Thị B", null, "Unpaid", null, 2 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Bills",
                keyColumn: "BillId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Bills",
                keyColumn: "BillId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Bills",
                keyColumn: "BillId",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Bills");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidAt",
                table: "Bills",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
