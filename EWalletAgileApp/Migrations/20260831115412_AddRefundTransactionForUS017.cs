using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EWalletAgileApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundTransactionForUS017 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatedTransactionId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Bills",
                keyColumn: "BillId",
                keyValue: 3,
                column: "CustomerName",
                value: "Nguyễn Văn A");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RelatedTransactionId",
                table: "Transactions",
                column: "RelatedTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_RelatedTransactionId",
                table: "Transactions",
                column: "RelatedTransactionId",
                principalTable: "Transactions",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_RelatedTransactionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_RelatedTransactionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RelatedTransactionId",
                table: "Transactions");

            migrationBuilder.UpdateData(
                table: "Bills",
                keyColumn: "BillId",
                keyValue: 3,
                column: "CustomerName",
                value: "Trần Thị B");
        }
    }
}
