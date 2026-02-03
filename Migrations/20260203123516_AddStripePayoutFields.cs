using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddStripePayoutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripePayoutId",
                table: "Withdrawals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeBankAccountId",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripePayoutId",
                table: "Withdrawals");

            migrationBuilder.DropColumn(
                name: "StripeBankAccountId",
                table: "InfluencerBankAccounts");
        }
    }
}
