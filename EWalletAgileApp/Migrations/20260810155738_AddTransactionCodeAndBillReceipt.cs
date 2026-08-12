using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EWalletAgileApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionCodeAndBillReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransactionCode",
                table: "Transactions",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql(@"
    UPDATE Transactions
    SET TransactionCode =
        'EW' +
        UPPER(
            LEFT(
                REPLACE(CONVERT(varchar(36), NEWID()), '-', ''),
                12
            )
        )
    WHERE TransactionCode = '';
");
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionCode",
                table: "Transactions",
                column: "TransactionCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TransactionCode",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransactionCode",
                table: "Transactions");
        }
    }
}
