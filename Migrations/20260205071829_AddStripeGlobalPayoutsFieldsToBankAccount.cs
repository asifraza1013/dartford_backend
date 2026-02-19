using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeGlobalPayoutsFieldsToBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripePayoutMethodId",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeRecipientAccountId",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripePayoutMethodId",
                table: "InfluencerBankAccounts");

            migrationBuilder.DropColumn(
                name: "StripeRecipientAccountId",
                table: "InfluencerBankAccounts");
        }
    }
}
