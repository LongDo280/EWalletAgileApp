using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EWalletAgileApp.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordSalt",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WalletStatus",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WalletStatusNote",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WalletStatusUpdatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    BankAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccountHolderName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnlinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.BankAccountId);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                columns: new[] { "FailedLoginCount", "LockedUntil", "Password", "PasswordSalt", "Role", "WalletStatus", "WalletStatusNote", "WalletStatusUpdatedAt" },
                values: new object[] { 0, null, "U03/hiz8j9IXqUH/pNgwOwKNfP8LDSby5gTEyBJFlg0=", "seed-salt-u1-9f3a", "User", "Active", null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 2,
                columns: new[] { "FailedLoginCount", "LockedUntil", "Password", "PasswordSalt", "Role", "WalletStatus", "WalletStatusNote", "WalletStatusUpdatedAt" },
                values: new object[] { 0, null, "u1VN9x4wx6xzhnMlfhTMtmHAM3dVwXd8SKbWMadWELo=", "seed-salt-u2-7c1e", "User", "Active", null, null });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "Balance", "CreatedAt", "DailyLimit", "Email", "FailedLoginCount", "FullName", "LockedUntil", "MaximumBalance", "Password", "PasswordSalt", "Phone", "PinCode", "Role", "Status", "TransactionLimit", "WalletStatus", "WalletStatusNote", "WalletStatusUpdatedAt" },
                values: new object[] { 3, 0m, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 50000000m, "admin@ewallet.com", 0, "Quản trị viên hệ thống", null, 100000000m, "Yrrw/UD5C2bN3Y6OGIZN1D2+JkEMK3t/jJ9umtZXus4=", "seed-salt-admin-2b8d", "0900000099", "9999", "Admin", "Active", 20000000m, "Active", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_UserId",
                table: "BankAccounts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordSalt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WalletStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WalletStatusNote",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WalletStatusUpdatedAt",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                column: "Password",
                value: "123456");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 2,
                column: "Password",
                value: "123456");
        }
    }
}
