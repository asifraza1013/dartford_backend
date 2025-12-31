using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddDualGatewayWithdrawalSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentGateway",
                table: "Withdrawals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TrueLayerBeneficiaryId",
                table: "Withdrawals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrueLayerPayoutId",
                table: "Withdrawals",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaystackRecipientCode",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentGateway",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TrueLayerBeneficiaryId",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentGateway",
                table: "Withdrawals");

            migrationBuilder.DropColumn(
                name: "TrueLayerBeneficiaryId",
                table: "Withdrawals");

            migrationBuilder.DropColumn(
                name: "TrueLayerPayoutId",
                table: "Withdrawals");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "InfluencerBankAccounts");

            migrationBuilder.DropColumn(
                name: "PaymentGateway",
                table: "InfluencerBankAccounts");

            migrationBuilder.DropColumn(
                name: "TrueLayerBeneficiaryId",
                table: "InfluencerBankAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "PaystackRecipientCode",
                table: "InfluencerBankAccounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
