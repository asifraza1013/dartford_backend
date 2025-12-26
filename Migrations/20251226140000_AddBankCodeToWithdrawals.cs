using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddBankCodeToWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankCode",
                table: "Withdrawals",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankCode",
                table: "Withdrawals");
        }
    }
}
